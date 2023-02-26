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

    public void Bind(Shader shader)
    {
        if (!LinkedHandles.ContainsKey(shader))
            LinkedHandles.TryAdd(shader, FileService.Parse<ShaderProgram>(shader.Source));
    }

    public void Bind(Texture texture)
    {
        if (!LinkedHandles.ContainsKey(texture))
            LinkedHandles.TryAdd(texture, FileService.Parse<TexImage2D>(texture.Source));
    }

    public void Bind(Mesh mesh)
    {
        if (!LinkedHandles.ContainsKey(mesh))
            LinkedHandles.TryAdd(mesh, FileService.Parse<VertexArrayObject>(mesh.Source));
    }

    public void Bind(MeshRenderer meshRenderer)
    {
        Bind(meshRenderer.Shader);
        Bind(meshRenderer.Texture);
        Bind(meshRenderer.Mesh);

        if (!LinkedHandles.ContainsKey(meshRenderer))
        {
            IRenderTarget renderTarget = GLContext.CreateRenderTarget();
            if (LinkedHandles.TryAdd(meshRenderer, renderTarget))
            {
                RenderTargets.Add(renderTarget);
                meshRenderer.Disposed += OnControlHandleDisposed;
            }
        }
    }

    private void OnControlHandleDisposed(object? sender, EventArgs e)
    {
        if (LinkedHandles.TryRemove(Unsafe.As<IHandle>(sender)!, out IHandle? internalHandle))
            internalHandle?.Dispose();
    }

    private void OnWindowResized(Vector2 newSize)
    {
        Camera.AspectRatio = newSize.GetRatio();
    }
}