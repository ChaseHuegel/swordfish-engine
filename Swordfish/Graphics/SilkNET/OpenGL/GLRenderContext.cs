using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Util;
using Shader = Swordfish.Graphics.SilkNET.Shader;

namespace Swordfish.Graphics;

internal class GLRenderContext : IRenderContext
{
    //  Reflects the Z axis.
    //  In openGL, positive Z is coming towards to viewer. We want it to extend away.
    private static readonly Matrix4x4 ReflectionMatrix = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, -1, 0,
        0, 0, 0, 1
    );

    private readonly ConcurrentBag<IRenderTarget> RenderTargets = new();

    private readonly ConcurrentDictionary<IHandle, IHandle> LinkedHandles = new();

    private readonly Camera Camera;

    private readonly GL GL;

    private readonly IWindowContext WindowContext;

    private readonly GLContext GLContext;

    private readonly IFileService FileService;

    private readonly BufferObject<Matrix4x4> InstanceBuffer;

    public unsafe GLRenderContext(GL gl, IWindowContext windowContext, GLContext glContext, IFileService fileService)
    {
        GL = gl;
        FileService = fileService;
        WindowContext = windowContext;
        GLContext = glContext;

        var transform = new Transform()
        {
            Position = new Vector3(0, 0, 5)
        };
        var models = new Matrix4x4[] {
            transform.ToMatrix4x4() * ReflectionMatrix
        };
        InstanceBuffer = new BufferObject<Matrix4x4>(gl, models, BufferTargetARB.ArrayBuffer);

        Camera = new Camera(90, WindowContext.GetSize().GetRatio(), 0.001f, 1000f);
        WindowContext.Loaded += OnWindowLoaded;
        WindowContext.Render += OnWindowRender;
        WindowContext.Resized += OnWindowResized;
    }

    private void OnWindowLoaded()
    {
        Debugger.Log("Renderer initialized.");
    }

    private unsafe void OnWindowRender(double delta)
    {
        var view = Camera.Transform.ToMatrix4x4() * ReflectionMatrix;
        var projection = Camera.GetProjection();

        if (RenderTargets.IsEmpty)
            return;

        //  TODO this will actually be per unique VAO
        var target = (GLRenderTarget)RenderTargets.First();

        Matrix4x4[] models = new Matrix4x4[RenderTargets.Count];
        IRenderTarget[] renderTargets = RenderTargets.ToArray();

        for (int i = 0; i < renderTargets.Length; i++)
            models[i] = renderTargets[i].Transform.ToMatrix4x4();

        target.ModelsArrayBufferObject.Bind();

        GL.GetBufferParameter(BufferTargetARB.ArrayBuffer, BufferPNameARB.Size, out int bufferSize);

        if (bufferSize >= models.Length * sizeof(Matrix4x4))
            GL.BufferSubData(BufferTargetARB.ArrayBuffer, 0, new ReadOnlySpan<Matrix4x4>(models));
        else
            GL.BufferData(BufferTargetARB.ArrayBuffer, new ReadOnlySpan<Matrix4x4>(models), BufferUsageARB.DynamicDraw);

        GL.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

        for (int i = 0; i < target.Materials.Length; i++)
        {
            GLMaterial material = target.Materials[i];
            material.Use();
            material.ShaderProgram.SetUniform("view", view);
            material.ShaderProgram.SetUniform("projection", projection);
        }

        target.VertexArrayObject.Bind();
        GL.DrawElementsInstanced(PrimitiveType.Triangles, (uint)target.ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0, (uint)models.Length);
    }

    public void Bind(Shader shader) => InternalBind(shader);
    public void Bind(Texture texture) => InternalBind(texture);
    public void Bind(Mesh mesh) => InternalBind(mesh);
    public void Bind(Material material) => InternalBind(material);
    public void Bind(MeshRenderer meshRenderer) => InternalBind(meshRenderer);

    private void OnControlHandleDisposed(object? sender, EventArgs e)
    {
        if (LinkedHandles.TryRemove(Unsafe.As<IHandle>(sender)!, out IHandle? internalHandle))
            internalHandle?.Dispose();
    }

    private void OnWindowResized(Vector2 newSize)
    {
        Camera.AspectRatio = newSize.GetRatio();
    }

    private ShaderProgram InternalBind(Shader shader)
    {
        if (!LinkedHandles.TryGetValue(shader, out IHandle? handle))
        {
            handle = FileService.Parse<ShaderProgram>(shader.Source);
            LinkedHandles.TryAdd(shader, handle);
        }

        return Unsafe.As<ShaderProgram>(handle);
    }

    private TexImage2D InternalBind(Texture texture)
    {
        if (!LinkedHandles.TryGetValue(texture, out IHandle? handle))
        {
            handle = FileService.Parse<TexImage2D>(texture.Source);
            LinkedHandles.TryAdd(texture, handle);
        }

        return Unsafe.As<TexImage2D>(handle);
    }

    private VertexArrayObject<float, uint> InternalBind(Mesh mesh)
    {
        if (!LinkedHandles.TryGetValue(mesh, out IHandle? handle))
        {
            handle = GLContext.CreateVertexArrayObject32(mesh.GetRawVertexData(), mesh.Triangles);
            LinkedHandles.TryAdd(mesh, handle);
        }

        return Unsafe.As<VertexArrayObject<float, uint>>(handle);
    }

    private GLMaterial InternalBind(Material material)
    {
        if (!LinkedHandles.TryGetValue(material, out IHandle? handle))
        {
            ShaderProgram shaderProgram = InternalBind(material.Shader);

            TexImage2D[] texImages2D = new TexImage2D[material.Textures.Length];
            for (int i = 0; i < material.Textures.Length; i++)
                texImages2D[i] = InternalBind(material.Textures[i]);

            handle = GLContext.CreateGLMaterial(shaderProgram, texImages2D);
            LinkedHandles.TryAdd(material, handle);
        }

        return Unsafe.As<GLMaterial>(handle);
    }

    private GLRenderTarget InternalBind(MeshRenderer meshRenderer)
    {
        if (!LinkedHandles.TryGetValue(meshRenderer, out IHandle? handle))
        {
            InternalBind(meshRenderer.Mesh);

            GLMaterial[] glMaterials = new GLMaterial[meshRenderer.Materials.Length];
            for (int i = 0; i < meshRenderer.Materials.Length; i++)
                glMaterials[i] = InternalBind(meshRenderer.Materials[i]);

            GLRenderTarget renderTarget = GLContext.CreateGLRenderTarget(
                meshRenderer.Transform,
                meshRenderer.Mesh.GetRawVertexData(),
                meshRenderer.Mesh.Triangles,
                glMaterials
            );
            handle = renderTarget;

            if (LinkedHandles.TryAdd(meshRenderer, renderTarget))
            {
                RenderTargets.Add(renderTarget);
                meshRenderer.Disposed += OnControlHandleDisposed;
            }
        }

        return Unsafe.As<GLRenderTarget>(handle);
    }
}