using System.Collections.Concurrent;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal class GLRenderContext : IRenderContext
{
    public DataBinding<Camera> Camera { get; set; } = new();

    public DataBinding<int> DrawCalls { get; } = new();

    internal readonly ConcurrentBag<GLRenderTarget> RenderTargets = new();
    private readonly ConcurrentDictionary<IHandle, IHandle> LinkedHandles = new();

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

        GL.ClearColor(Color.CornflowerBlue);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);

        Camera.Set(new Camera(90, WindowContext.GetSize().GetRatio(), 0.001f, 1000f));
        WindowContext.Resized += OnWindowResized;
        WindowContext.Render += OnWindowRender;

        for (int i = 0; i < Renderers.Length; i++)
        {
            Renderers[i].Initialize(this);
        }

        Debugger.Log("Renderer initialized.");
    }

    public void Bind(Shader shader) => BindShader(shader);
    public void Bind(Texture texture) => BindTexture(texture);
    public void Bind(Mesh mesh) => BindMesh(mesh);
    public void Bind(Material material) => BindMaterial(material);
    public void Bind(MeshRenderer meshRenderer) => BindMeshRenderer(meshRenderer);

    private void OnHandleDisposed(object? sender, EventArgs e)
    {
        if (LinkedHandles.TryRemove(Unsafe.As<IHandle>(sender)!, out IHandle? internalHandle))
            internalHandle?.Dispose();
    }

    private void OnWindowResized(Vector2 newSize)
    {
        Camera.Get().AspectRatio = newSize.GetRatio();
    }

    private void OnWindowRender(double delta)
    {

        Camera camera = Camera.Get();
        Matrix4x4 view = camera.GetView();
        Matrix4x4 projection = camera.GetProjection();

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        for (int i = 0; i < Renderers.Length; i++)
        {
            Renderers[i].PreRender(delta, view, projection);
        }

        int drawCalls = 0;
        for (int i = 0; i < Renderers.Length; i++)
        {
            drawCalls += Renderers[i].Render(delta, view, projection);
        }
        DrawCalls.Set(drawCalls);
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

            handle = GLContext.CreateGLMaterial(shaderProgram, textures, material.Transparent);
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
                meshRenderer.Disposed += OnHandleDisposed;
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