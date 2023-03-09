using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using Swordfish.Graphics.SilkNET;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;

namespace Swordfish.Graphics;

internal class RenderContext : IRenderContext
{
    private readonly ConcurrentBag<IRenderTarget> RenderTargets = new();

    private readonly ConcurrentDictionary<IHandle, IHandle> LinkedHandles = new();

    private readonly Camera Camera;

    private readonly IWindowContext WindowContext;

    private readonly GLContext GLContext;

    private readonly IFileService FileService;

    public RenderContext(IWindowContext windowContext, GLContext glContext, IFileService fileService)
    {
        FileService = fileService;
        WindowContext = windowContext;
        GLContext = glContext;

        Camera = new Camera(90, WindowContext.GetSize().GetRatio(), 0.001f, 1000f);
        WindowContext.Resized += OnWindowResized;
    }

    public void Initialize()
    {
        Debugger.Log("Renderer initialized.");
    }

    public void Render(double delta)
    {
        foreach (IRenderTarget target in RenderTargets)
        {
            target.Render(Camera);
        }
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

    private VertexArrayObject InternalBind(Mesh mesh)
    {
        if (!LinkedHandles.TryGetValue(mesh, out IHandle? handle))
        {
            handle = FileService.Parse<VertexArrayObject>(mesh.Source);
            LinkedHandles.TryAdd(mesh, handle);
        }

        return Unsafe.As<VertexArrayObject>(handle);
    }

    private GLMaterial InternalBind(Material material)
    {
        if (!LinkedHandles.TryGetValue(material, out IHandle? handle))
        {
            ShaderProgram shaderProgram = InternalBind(material.Shader);

            List<TexImage2D> texImages2D = new();
            for (int i = 0; i < material.Textures.Length; i++)
                texImages2D.Add(InternalBind(material.Textures[i]));

            handle = GLContext.CreateGLMaterial(shaderProgram, texImages2D);
            LinkedHandles.TryAdd(material, handle);
        }

        return Unsafe.As<GLMaterial>(handle);
    }

    private GLRenderTarget InternalBind(MeshRenderer meshRenderer)
    {
        if (!LinkedHandles.TryGetValue(meshRenderer, out IHandle? handle))
        {
            Bind(meshRenderer.Mesh);

            for (int i = 0; i < meshRenderer.Materials.Length; i++)
                Bind(meshRenderer.Materials[i]);

            GLRenderTarget renderTarget = GLContext.CreateGLRenderTarget();
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