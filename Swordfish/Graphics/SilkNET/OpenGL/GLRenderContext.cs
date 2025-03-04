using System.Collections.Concurrent;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using Shoal.DependencyInjection;
using Silk.NET.OpenGL;
using Swordfish.Library.Extensions;
using Swordfish.Library.Types;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Graphics.SilkNET.OpenGL;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class GLRenderContext : IRenderContext, IDisposable, IAutoActivate
{
    public DataBinding<Camera> Camera { get; set; } = new();

    public DataBinding<int> DrawCalls { get; } = new();

    internal readonly ConcurrentBag<GLRenderTarget> RenderTargets = new();
    private readonly ConcurrentDictionary<IHandle, IHandle> _linkedHandles = new();

    private readonly GL _gl;
    private readonly IWindowContext _windowContext;
    private readonly GLContext _glContext;
    private readonly IRenderStage[] _renderers;

    public GLRenderContext(GL gl, IWindowContext windowContext, GLContext glContext, IRenderStage[] renderers)
    {
        _gl = gl;
        _windowContext = windowContext;
        _glContext = glContext;
        _renderers = renderers;

        _gl.ClearColor(Color.FromArgb(20, 21, 37));
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

        Camera.Set(new Camera(90, _windowContext.GetSize().GetRatio(), 0.001f, 1000f));
        _windowContext.Resized += OnWindowResized;
        _windowContext.Render += OnWindowRender;

        for (var i = 0; i < _renderers.Length; i++)
        {
            //  TODO there has to be a better way to do this without a circular dependency
            _renderers[i].Initialize(this);
        }
    }
    
    public void Dispose()
    {
        _windowContext.Resized -= OnWindowResized;
        _windowContext.Render -= OnWindowRender;
    }

    public void Bind(Shader shader) => BindShader(shader);
    public void Bind(Texture texture) => BindTexture(texture);
    public void Bind(Mesh mesh) => BindMesh(mesh);
    public void Bind(Material material) => BindMaterial(material);
    public void Bind(MeshRenderer meshRenderer) => BindMeshRenderer(meshRenderer);

    private void OnHandleDisposed(object? sender, EventArgs e)
    {
        if (_linkedHandles.TryRemove(Unsafe.As<IHandle>(sender)!, out IHandle? internalHandle))
        {
            internalHandle?.Dispose();
        }
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

        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        for (var i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].PreRender(delta, view, projection);
        }

        var drawCalls = 0;
        for (var i = 0; i < _renderers.Length; i++)
        {
            drawCalls += _renderers[i].Render(delta, view, projection);
        }
        DrawCalls.Set(drawCalls);
    }

    private ShaderComponent BindShaderSource(ShaderSource shaderSource)
    {
        if (_linkedHandles.TryGetValue(shaderSource, out IHandle? handle))
        {
            return Unsafe.As<ShaderComponent>(handle);
        }

        handle = _glContext.CreateShaderComponent(shaderSource.Name, shaderSource.Type.ToSilkShaderType(), shaderSource.Source);
        _linkedHandles.TryAdd(shaderSource, handle);
        return Unsafe.As<ShaderComponent>(handle);
    }

    private ShaderProgram BindShader(Shader shader)
    {
        if (_linkedHandles.TryGetValue(shader, out IHandle? handle))
        {
            return Unsafe.As<ShaderProgram>(handle);
        }

        ShaderComponent[] shaderComponents = shader.Sources.Select(BindShaderSource).ToArray();
        handle = _glContext.CreateShaderProgram(shader.Name, shaderComponents);
        _linkedHandles.TryAdd(shader, handle);
        return Unsafe.As<ShaderProgram>(handle);
    }

    private IGLTexture BindTexture(Texture texture)
    {
        if (_linkedHandles.TryGetValue(texture, out IHandle? handle))
        {
            return Unsafe.As<IGLTexture>(handle);
        }

        if (texture is TextureArray textureArray)
        {
            handle = _glContext.CreateTexImage3D(textureArray.Name, textureArray.Pixels, (uint)textureArray.Width, (uint)textureArray.Height, (uint)textureArray.Depth, textureArray.Mipmaps);
        }
        else
        {
            handle = _glContext.CreateTexImage2D(texture.Name, texture.Pixels, (uint)texture.Width, (uint)texture.Height, texture.Mipmaps);
        }

        _linkedHandles.TryAdd(texture, handle);
        return Unsafe.As<IGLTexture>(handle);
    }

    private VertexArrayObject<float, uint> BindMesh(Mesh mesh)
    {
        if (_linkedHandles.TryGetValue(mesh, out IHandle? handle))
        {
            return Unsafe.As<VertexArrayObject<float, uint>>(handle);
        }

        handle = _glContext.CreateVertexArrayObject32(mesh.GetRawVertexData(), mesh.Triangles);
        _linkedHandles.TryAdd(mesh, handle);
        return Unsafe.As<VertexArrayObject<float, uint>>(handle);
    }

    private GLMaterial BindMaterial(Material material)
    {
        if (_linkedHandles.TryGetValue(material, out IHandle? handle))
        {
            return Unsafe.As<GLMaterial>(handle);
        }

        ShaderProgram shaderProgram = BindShader(material.Shader);

        var textures = new IGLTexture[material.Textures.Length];
        for (var i = 0; i < material.Textures.Length; i++)
        {
            textures[i] = BindTexture(material.Textures[i]);
        }

        handle = _glContext.CreateGLMaterial(shaderProgram, textures, material.Transparent);
        _linkedHandles.TryAdd(material, handle);
        return Unsafe.As<GLMaterial>(handle);
    }

    private void BindMeshRenderer(MeshRenderer meshRenderer)
    {
        if (_linkedHandles.TryGetValue(meshRenderer, out IHandle? _))
        {
            return;
        }

        VertexArrayObject<float, uint> vao = BindMesh(meshRenderer.Mesh);
        BufferObject<Matrix4x4> mbo = BindToMbo(vao);

        var glMaterials = new GLMaterial[meshRenderer.Materials.Length];
        for (var i = 0; i < meshRenderer.Materials.Length; i++)
        {
            glMaterials[i] = BindMaterial(meshRenderer.Materials[i]);
        }

        GLRenderTarget renderTarget = _glContext.CreateGLRenderTarget(
            meshRenderer.Transform,
            vao,
            mbo,
            glMaterials,
            meshRenderer.RenderOptions
        );

        if (!_linkedHandles.TryAdd(meshRenderer, renderTarget))
        {
            return;
        }

        RenderTargets.Add(renderTarget);
        meshRenderer.Disposed += OnHandleDisposed;
    }

    private BufferObject<Matrix4x4> BindToMbo(VertexArrayObject<float, uint> vao)
    {
        if (_linkedHandles.TryGetValue(vao, out IHandle? handle))
        {
            return Unsafe.As<BufferObject<Matrix4x4>>(handle);
        }

        handle = _glContext.CreateBufferObject(Array.Empty<Matrix4x4>(), BufferTargetARB.ArrayBuffer, BufferUsageARB.DynamicDraw);
        _linkedHandles.TryAdd(vao, handle);
        return Unsafe.As<BufferObject<Matrix4x4>>(handle);
    }
}