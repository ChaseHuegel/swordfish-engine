using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using Shoal.DependencyInjection;
using Silk.NET.OpenGL;
using Swordfish.ECS;
using Swordfish.Graphics.SilkNET.OpenGL.Renderers;
using Swordfish.Library.Collections;
using Swordfish.Library.Extensions;
using Swordfish.Library.Types;

// ReSharper disable UnusedMember.Global

namespace Swordfish.Graphics.SilkNET.OpenGL;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class GLRenderContext : IRenderContext, IDisposable, IAutoActivate, IEntitySystem
{
    public int Order => 100_002;

    public DataBinding<Camera> MainCamera { get; } = new();
    
    public DataBinding<int> DrawCalls { get; } = new();

    internal readonly LockedList<GLRenderTarget> RenderTargets = new();
    internal readonly LockedList<GLRectRenderTarget> RectRenderTargets = new();
    private readonly ConcurrentDictionary<IHandle, IHandle> _linkedHandles = new();

    private readonly GL _gl;
    private readonly IWindowContext _windowContext;
    private readonly GLContext _glContext;
    private readonly IRenderPipeline[] _renderPipelines;
    private readonly SynchronizationContext _synchronizationContext;
    
    private readonly DoubleList<RenderInstance> _renderInstancesBuffer = new();

    private Matrix4x4 _cameraView;
    private Matrix4x4 _cameraProjection;
    private float _aspectRatio;

    public GLRenderContext(
        GL gl,
        IWindowContext windowContext,
        GLContext glContext,
        IRenderPipeline[] renderPipelines,
        IRenderStage[] renderers,
        SynchronizationContext synchronizationContext
    ) {
        _gl = gl;
        _windowContext = windowContext;
        _glContext = glContext;
        _renderPipelines = renderPipelines;
        _synchronizationContext = synchronizationContext;

        _aspectRatio = _windowContext.GetSize().GetRatio();
        
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
        gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        
        for (var i = 0; i < renderers.Length; i++)
        {
            //  TODO there has to be a better way to do this without a circular dependency
            renderers[i].Initialize(this);
        }
        
        _windowContext.Resized += OnWindowResized;
        _windowContext.Render += OnWindowRender;
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
    public void Bind(MeshRenderer meshRenderer, int entity) => BindMeshRenderer(meshRenderer, entity);
    public void Bind(RectRenderer rectRenderer) => BindRectRenderer(rectRenderer);

    private void OnHandleDisposed(object? sender, EventArgs _)
    {
        if (_linkedHandles.TryRemove(Unsafe.As<IHandle>(sender)!, out IHandle? internalHandle))
        {
            _synchronizationContext.Post(_ => internalHandle.Dispose(), null);
        }
    }

    public void Tick(float delta, DataStore store)
    {
        lock (_renderInstancesBuffer)
        {
            store.Query<TransformComponent, ViewFrustumComponent>(QueryCamera);
            
            _renderInstancesBuffer.Clear();
            store.Query<TransformComponent, MeshRendererComponent>(QueryRenderableEntities);
        }
    }

    private void QueryCamera(float delta, DataStore store, int entity, ref TransformComponent transform, ref ViewFrustumComponent viewFrustum)
    {
        var camera = new Camera(viewFrustum, transform);
        MainCamera.Set(camera);
        
        _cameraView = camera.GetView();
        _cameraProjection = Matrix4x4.CreatePerspectiveFieldOfView(camera.ViewFrustum.Fov.Radians, _aspectRatio, viewFrustum.NearPlane, viewFrustum.FarPlane);
    }

    private void QueryRenderableEntities(float f, DataStore store, int entity, ref TransformComponent transform, ref MeshRendererComponent meshRendererComponent)
    {
        if (meshRendererComponent.MeshRenderer == null)
        {
            return;
        }
        
        _renderInstancesBuffer.Write(new RenderInstance(entity, transform.ToMatrix4X4()));
    }

    private void OnWindowResized(Vector2 newSize)
    {
        _aspectRatio = newSize.GetRatio();
    }

    private void OnWindowRender(double delta)
    {
        Matrix4x4 view;
        Matrix4x4 projection;
        RenderInstance[] instances;
        lock (_renderInstancesBuffer)
        {
            view = _cameraView;
            projection = _cameraProjection;
            _renderInstancesBuffer.Swap();
            instances = _renderInstancesBuffer.Read();
        }
        
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        var drawCalls = 0;
        for (var i = 0; i < _renderPipelines.Length; i++)
        {
            drawCalls += _renderPipelines[i].Render(delta, view, projection, instances);
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
        if (_linkedHandles.TryAdd(shaderSource, handle))
        {
            shaderSource.Disposed += OnHandleDisposed;
        }
        
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
        if (_linkedHandles.TryAdd(shader, handle))
        {
            shader.Disposed += OnHandleDisposed;
        }
        
        return Unsafe.As<ShaderProgram>(handle);
    }

    private IGLTexture BindTexture(Texture texture)
    {
        if (_linkedHandles.TryGetValue(texture, out IHandle? handle))
        {
            return Unsafe.As<IGLTexture>(handle);
        }

        TextureFormat format = TextureFormat.Rgba;
        TextureParams @params = TextureParams.ClampNearest with
        {
            GenerateMipmaps = texture.Mipmaps,
        };
        
        if (texture is TextureArray textureArray)
        {
            handle = _glContext.CreateTexImage3D(textureArray.Name, textureArray.Pixels, (uint)textureArray.Width, (uint)textureArray.Height, (uint)textureArray.Depth, format, @params);
        }
        else
        {
            handle = _glContext.CreateTexImage2D(texture.Name, texture.Pixels, (uint)texture.Width, (uint)texture.Height, format, @params);
        }

        if (_linkedHandles.TryAdd(texture, handle))
        {
            texture.Disposed += OnHandleDisposed;
        }
        
        return Unsafe.As<IGLTexture>(handle);
    }

    private VertexArrayObject<float, uint> BindMesh(Mesh mesh)
    {
        if (_linkedHandles.TryGetValue(mesh, out IHandle? handle))
        {
            return Unsafe.As<VertexArrayObject<float, uint>>(handle);
        }

        handle = _glContext.CreateVertexArrayObject32(mesh.GetRawVertexData(), mesh.Triangles);
        if (_linkedHandles.TryAdd(mesh, handle))
        {
            mesh.Disposed += OnHandleDisposed;
        }

        return Unsafe.As<VertexArrayObject<float, uint>>(handle);
    }

    internal GLMaterial BindMaterial(Material material)
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
        if (_linkedHandles.TryAdd(material, handle))
        {
            material.Disposed += OnHandleDisposed;
        }
        
        return Unsafe.As<GLMaterial>(handle);
    }

    private void BindMeshRenderer(MeshRenderer meshRenderer, int entity)
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
            entity,
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
        meshRenderer.Disposed += OnMeshRendererDisposed;
        return;

        void OnMeshRendererDisposed(object? sender, EventArgs e)
        {
            RenderTargets.Remove(renderTarget);
            OnHandleDisposed(sender, e);
        }
    }
    
    private void BindRectRenderer(RectRenderer rectRenderer)
    {
        if (_linkedHandles.TryGetValue(rectRenderer, out IHandle? _))
        {
            return;
        }

        var glMaterials = new GLMaterial[rectRenderer.Materials.Length];
        for (var i = 0; i < rectRenderer.Materials.Length; i++)
        {
            glMaterials[i] = BindMaterial(rectRenderer.Materials[i]);
        }

        GLRectRenderTarget renderTarget = _glContext.CreateGLRectRenderTarget(
            rectRenderer.Rect,
            rectRenderer.Color.ToVector4(),
            glMaterials
        );

        if (!_linkedHandles.TryAdd(rectRenderer, renderTarget))
        {
            return;
        }

        RectRenderTargets.Add(renderTarget);
        rectRenderer.Disposed += OnRectRendererDisposed;
        return;

        void OnRectRendererDisposed(object? sender, EventArgs e)
        {
            RectRenderTargets.Remove(renderTarget);
            OnHandleDisposed(sender, e);
        }
    }

    private BufferObject<Matrix4x4> BindToMbo(VertexArrayObject<float, uint> vao)
    {
        if (_linkedHandles.TryGetValue(vao, out IHandle? handle))
        {
            return Unsafe.As<BufferObject<Matrix4x4>>(handle);
        }

        handle = _glContext.CreateBufferObject(Array.Empty<Matrix4x4>(), BufferTargetARB.ArrayBuffer, BufferUsageARB.DynamicDraw);
        if (_linkedHandles.TryAdd(vao, handle))
        {
            vao.Disposed += OnHandleDisposed;
        }
        
        return Unsafe.As<BufferObject<Matrix4x4>>(handle);
    }
}