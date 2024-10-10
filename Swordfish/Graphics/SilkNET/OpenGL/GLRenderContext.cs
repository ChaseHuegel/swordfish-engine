using System.Collections.Concurrent;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Types;

namespace Swordfish.Graphics;

internal class GLRenderContext : IRenderContext
{
    public DataBinding<Camera> Camera { get; set; } = new();

    public DataBinding<int> DrawCalls { get; } = new();

    public DataBinding<bool> Wireframe { get; set; } = new();

    private readonly ConcurrentBag<GLRenderTarget> RenderTargets = new();
    private readonly ConcurrentDictionary<IHandle, IHandle> LinkedHandles = new();
    private ConcurrentDictionary<GLRenderTarget, ConcurrentBag<Matrix4x4>> InstancedRenderTargets = new();

    private readonly Transform ViewTransform = new();

    private readonly GL GL;
    private readonly IWindowContext WindowContext;
    private readonly GLContext GLContext;
    private readonly IRenderStage[] Renderers;

    public unsafe GLRenderContext(GL gl, IWindowContext windowContext, GLContext glContext, IRenderStage[] renderers)
    {
        GL = gl;
        WindowContext = windowContext;
        GLContext = glContext;
        Renderers = renderers;

        GL.FrontFace(FrontFaceDirection.CW);
        //  TODO gamma correction should be handled via a post processing shader so its tunable
        // GL.Enable(GLEnum.FramebufferSrgb);

        Camera.Set(new Camera(90, WindowContext.GetSize().GetRatio(), 0.001f, 1000f));
        WindowContext.Render += OnWindowRender;
        WindowContext.Resized += OnWindowResized;

        for (int i = 0; i < Renderers.Length; i++)
        {
            Renderers[i].Load();
        }

        Debugger.Log("Renderer initialized.");
    }


    private void OnWindowRender(double delta)
    {
        int drawCalls = 0;
        if (RenderTargets.IsEmpty)
            return;

        Camera cameraCached = Camera.Get();
        ViewTransform.Position = cameraCached.Transform.Position;
        ViewTransform.Rotation = cameraCached.Transform.Rotation;
        //  Reflect the camera's Z scale so +Z extends away from the viewer
        ViewTransform.Scale = new Vector3(cameraCached.Transform.Scale.X, cameraCached.Transform.Scale.Y, -cameraCached.Transform.Scale.Z);

        Matrix4x4.Invert(ViewTransform.ToMatrix4x4(), out Matrix4x4 view);
        Matrix4x4 projection = Camera.Get().GetProjection();

        GL.ClearColor(Color.CornflowerBlue);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Back);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);

        drawCalls += RenderInstancedTargets(view, projection);

        for (int i = 0; i < Renderers.Length; i++)
        {
            drawCalls += Renderers[i].Render(delta, view, projection);
        }

        DrawCalls.Set(drawCalls);
    }

    private unsafe int RenderInstancedTargets(Matrix4x4 view, Matrix4x4 projection)
    {
        int drawCalls = 0;
        foreach (var instancedTarget in InstancedRenderTargets)
        {
            GLRenderTarget target = instancedTarget.Key;
            Matrix4x4[] models = instancedTarget.Value.ToArray();

            target.ModelsBufferObject.Bind();

            GL.GetBufferParameter(BufferTargetARB.ArrayBuffer, BufferPNameARB.Size, out int bufferSize);

            if (bufferSize >= models.Length * sizeof(Matrix4x4))
                GL.BufferSubData(BufferTargetARB.ArrayBuffer, 0, new ReadOnlySpan<Matrix4x4>(models));
            else
                GL.BufferData(BufferTargetARB.ArrayBuffer, new ReadOnlySpan<Matrix4x4>(models), BufferUsageARB.DynamicDraw);

            GL.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

            for (int n = 0; n < target.Materials.Length; n++)
            {
                GLMaterial material = target.Materials[n];
                ShaderProgram shader = material.ShaderProgram;
                material.Use();

                shader.SetUniform("view", view);
                shader.SetUniform("projection", projection);
            }

            target.VertexArrayObject.Bind();

            GL.Set(EnableCap.DepthTest, !target.RenderOptions.IgnoreDepth);
            GL.Set(EnableCap.CullFace, !target.RenderOptions.DoubleFaced);
            GL.PolygonMode(MaterialFace.FrontAndBack, Wireframe || target.RenderOptions.Wireframe ? PolygonMode.Line : PolygonMode.Fill);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, (uint)target.VertexArrayObject.ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0, (uint)models.Length);
            drawCalls++;
        }

        return drawCalls;
    }

    public void RefreshRenderTargets()
    {
        var instanceMap = new ConcurrentDictionary<GLRenderTarget, ConcurrentBag<Matrix4x4>>();
        foreach (GLRenderTarget renderTarget in RenderTargets)
        {
            if (!instanceMap.TryGetValue(renderTarget, out ConcurrentBag<Matrix4x4>? matrices))
            {
                matrices = new ConcurrentBag<Matrix4x4>();
                instanceMap.TryAdd(renderTarget, matrices);
            }

            matrices.Add(renderTarget.Transform.ToMatrix4x4());
        }

        InstancedRenderTargets = instanceMap;
    }

    public void Bind(Shader shader) => BindShader(shader);
    public void Bind(Texture texture) => BindTexture(texture);
    public void Bind(Mesh mesh) => BindMesh(mesh);
    public void Bind(Material material) => BindMaterial(material);
    public void Bind(MeshRenderer meshRenderer) => BindMeshRenderer(meshRenderer);

    private void OnControlHandleDisposed(object? sender, EventArgs e)
    {
        if (LinkedHandles.TryRemove(Unsafe.As<IHandle>(sender)!, out IHandle? internalHandle))
            internalHandle?.Dispose();
    }

    private void OnWindowResized(Vector2 newSize)
    {
        Camera.Get().AspectRatio = newSize.GetRatio();
    }

    internal ShaderComponent BindShaderSource(ShaderSource shaderSource)
    {
        if (!LinkedHandles.TryGetValue(shaderSource, out IHandle? handle))
        {
            handle = GLContext.CreateShaderComponent(shaderSource.Name, shaderSource.Type.ToSilkShaderType(), shaderSource.Source);
            LinkedHandles.TryAdd(shaderSource, handle);
        }

        return Unsafe.As<ShaderComponent>(handle);
    }

    internal ShaderProgram BindShader(Shader shader)
    {
        if (!LinkedHandles.TryGetValue(shader, out IHandle? handle))
        {
            ShaderComponent[] shaderComponents = shader.Sources.Select(BindShaderSource).ToArray();
            handle = GLContext.CreateShaderProgram(shader.Name, shaderComponents);
            LinkedHandles.TryAdd(shader, handle);
        }

        return Unsafe.As<ShaderProgram>(handle);
    }

    internal IGLTexture BindTexture(Texture texture)
    {
        if (!LinkedHandles.TryGetValue(texture, out IHandle? handle))
        {
            if (texture is TextureArray textureArray)
                handle = GLContext.CreateTexImage3D(textureArray.Name, textureArray.Pixels, (uint)textureArray.Width, (uint)textureArray.Height, (uint)textureArray.Depth, textureArray.Mipmaps);
            else
                handle = GLContext.CreateTexImage2D(texture.Name, texture.Pixels, (uint)texture.Width, (uint)texture.Height, texture.Mipmaps);

            LinkedHandles.TryAdd(texture, handle);
        }

        return Unsafe.As<IGLTexture>(handle);
    }

    internal VertexArrayObject<float, uint> BindMesh(Mesh mesh)
    {
        if (!LinkedHandles.TryGetValue(mesh, out IHandle? handle))
        {
            handle = GLContext.CreateVertexArrayObject32(mesh.GetRawVertexData(), mesh.Triangles);
            LinkedHandles.TryAdd(mesh, handle);
        }

        return Unsafe.As<VertexArrayObject<float, uint>>(handle);
    }

    internal GLMaterial BindMaterial(Material material)
    {
        if (!LinkedHandles.TryGetValue(material, out IHandle? handle))
        {
            ShaderProgram shaderProgram = BindShader(material.Shader);

            IGLTexture[] textures = new IGLTexture[material.Textures.Length];
            for (int i = 0; i < material.Textures.Length; i++)
                textures[i] = BindTexture(material.Textures[i]);

            handle = GLContext.CreateGLMaterial(shaderProgram, textures);
            LinkedHandles.TryAdd(material, handle);
        }

        return Unsafe.As<GLMaterial>(handle);
    }

    internal unsafe GLRenderTarget BindMeshRenderer(MeshRenderer meshRenderer)
    {
        if (!LinkedHandles.TryGetValue(meshRenderer, out IHandle? handle))
        {
            VertexArrayObject<float, uint> vao = BindMesh(meshRenderer.Mesh);
            BufferObject<Matrix4x4> mbo = BindToMBO(vao);

            GLMaterial[] glMaterials = new GLMaterial[meshRenderer.Materials.Length];
            for (int i = 0; i < meshRenderer.Materials.Length; i++)
                glMaterials[i] = BindMaterial(meshRenderer.Materials[i]);

            GLRenderTarget renderTarget = GLContext.CreateGLRenderTarget(
                meshRenderer.Transform,
                vao,
                mbo,
                glMaterials,
                meshRenderer.RenderOptions
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

    internal BufferObject<Matrix4x4> BindToMBO(VertexArrayObject<float, uint> vao)
    {
        if (!LinkedHandles.TryGetValue(vao, out IHandle? handle))
        {
            handle = GLContext.CreateBufferObject(Array.Empty<Matrix4x4>(), BufferTargetARB.ArrayBuffer, BufferUsageARB.DynamicDraw);
            LinkedHandles.TryAdd(vao, handle);
        }

        return Unsafe.As<BufferObject<Matrix4x4>>(handle);
    }
}