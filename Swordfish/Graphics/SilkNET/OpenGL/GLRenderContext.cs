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
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLRenderContext : IRenderContext, IDisposable, IAutoActivate, IEntitySystem
{
    public DataBinding<CameraEntity> MainCamera { get; } = new();
    public DataBinding<int> DrawCalls { get; } = new();

    private readonly LockedList<GLRenderTarget> _renderTargets = [];
    private readonly LockedList<GLRectRenderTarget> _rectRenderTargets = [];
    private readonly ConcurrentDictionary<IHandle, IHandle> _linkedHandles = new();
    private readonly ConcurrentQueue<UnboundRenderer> _unboundRenderers = new();
    private readonly List<EntityModel> _renderableEntities = [];

    private readonly GLContext _glContext;
    private readonly IWindowContext _windowContext;
    private readonly SynchronizationContext _synchronizationContext;
    private readonly RenderSettings _renderSettings;

    private SceneData _sceneData = SceneData.Empty;
    private float _windowAspectRatio;

    public GLRenderContext(
        in GLContext glContext,
        in IWindowContext windowContext,
        in SynchronizationContext synchronizationContext,
        in RenderSettings renderSettings
    ) {
        _glContext = glContext;
        _windowContext = windowContext;
        _synchronizationContext = synchronizationContext;
        _renderSettings = renderSettings;

        _windowContext.Resized += OnWindowResized;
        _windowAspectRatio = windowContext.Resolution.GetRatio();
    }

    public void Dispose()
    {
        _windowContext.Resized -= OnWindowResized;
    }

    public void Bind(Shader shader) => BindShader(shader);
    public void Bind(Texture texture) => BindTexture(texture);
    public void Bind(Mesh mesh) => BindMesh(mesh);
    public void Bind(Material material) => BindMaterial(material);
    public void Bind(MeshRenderer meshRenderer, Uuid entity) => BindMeshRenderer(meshRenderer, entity);
    public void Bind(RectRenderer rectRenderer) => BindRectRenderer(rectRenderer);

    bool IRenderContext.TryDequeueUnbound(out UnboundRenderer unbound) => _unboundRenderers.TryDequeue(out unbound);

    public bool TryGetRenderTarget(MeshRenderer renderer, out GLRenderTarget? target)
    {
        if (_linkedHandles.TryGetValue(renderer, out IHandle? handle))
        {
            target = Unsafe.As<GLRenderTarget>(handle);
            return true;
        }

        target = null;
        return false;
    }

    public RenderScene GetSceneContext()
    {
        SceneData scene = Volatile.Read(ref _sceneData);
        return new RenderScene(scene.View, scene.Projection, scene.Instances, _renderTargets, _rectRenderTargets);
    }

    private void OnHandleDisposed(object? sender, EventArgs _)
    {
        if (sender is not IHandle handle)
        {
            return;
        }

        _synchronizationContext.Post(_ => DisposeHandle(handle), null);
    }

    private void DisposeHandle(IHandle handle)
    {
        if (_linkedHandles.TryRemove(handle, out IHandle? internalHandle))
        {
            internalHandle.Dispose();
        }
    }

    public void Tick(float delta, DataStore store)
    {
        //  Since the data is only written in here, it's safe to read
        Matrix4x4 cameraView = _sceneData.View;
        Matrix4x4 cameraProjection = _sceneData.Projection;

        //  Collect the camera's state
        var queryCameraAction = new ReadCameraAction(this, cameraView, cameraProjection);
        store.QueryRef<CameraComponent, ViewFrustumComponent, ReadCameraAction>(0f, ref queryCameraAction);
        cameraView = queryCameraAction.View;
        cameraProjection = queryCameraAction.Projection;

        Frustum? frustum = queryCameraAction.Found ? Frustum.FromViewProjection(cameraView * cameraProjection) : null;

        //  Collect all renderable entities
        var queryRenderableEntitiesAction = new ReadRenderableEntityAction(this, frustum);
        store.Query<TransformComponent, MeshRendererComponent, ReadRenderableEntityAction>(0f, ref queryRenderableEntitiesAction);

        //  Write the scene data then reset for the next Tick
        Volatile.Write(ref _sceneData, new SceneData(cameraView, cameraProjection, _renderableEntities.ToArray()));
        _renderableEntities.Clear();
    }

    private void OnWindowResized(Vector2 newSize)
    {
        _windowAspectRatio = newSize.GetRatio();
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
        else if (texture is TextureCubemap textureCubemap)
        {
            var pixels = new byte[6][];
            for (var i = 0; i < textureCubemap.Textures.Length; i++)
            {
                pixels[i] = textureCubemap.Textures[i].Pixels;
            }
            handle = _glContext.CreateTexCubemap(textureCubemap.Name, pixels, (uint)textureCubemap.Width, (uint)textureCubemap.Height, format, @params);
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

    private VertexArrayObject<float, uint>? BindMesh(Mesh mesh)
    {
        if (mesh.IsDisposed)
        {
            return null;
        }

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

    private void BindMeshRenderer(MeshRenderer meshRenderer, Uuid entity)
    {
        if (meshRenderer.IsDisposed)
        {
            return;
        }

        if (_linkedHandles.TryGetValue(meshRenderer, out IHandle? _))
        {
            return;
        }

        VertexArrayObject<float, uint>? vao = BindMesh(meshRenderer.Mesh);
        if (vao == null)
        {
            return;
        }

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

        _renderTargets.Add(renderTarget);
        meshRenderer.Disposed += OnMeshRendererDisposed;
        meshRenderer.IsBound = true;
        return;

        void OnMeshRendererDisposed(object? sender, EventArgs e)
        {
            _renderTargets.Remove(renderTarget);
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

        _rectRenderTargets.Add(renderTarget);
        rectRenderer.Disposed += OnRectRendererDisposed;
        return;

        void OnRectRendererDisposed(object? sender, EventArgs e)
        {
            _rectRenderTargets.Remove(renderTarget);
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
    
    private sealed class SceneData(in Matrix4x4 view, in Matrix4x4 projection, in EntityModel[] instances)
    {
        public static readonly SceneData Empty = new(Matrix4x4.Identity, Matrix4x4.Identity, []);

        public readonly Matrix4x4 View = view;
        public readonly Matrix4x4 Projection = projection;
        public readonly EntityModel[] Instances = instances;
    }
    
    private struct ReadCameraAction(in GLRenderContext owner, in Matrix4x4 view, in Matrix4x4 projection) : IForEachRef<CameraComponent, ViewFrustumComponent>
    {
        private readonly GLRenderContext _owner = owner;
        public Matrix4x4 View = view;
        public Matrix4x4 Projection = projection;
        public bool Found;

        public void Execute(float delta, DataStore store, int entity, ref Ref<CameraComponent> camera, ref Ref<ViewFrustumComponent> viewFrustum)
        {
            if (!store.TryGet(entity, out TransformComponent transform))
            {
                return;
            }

            Found = true;
            viewFrustum.Write.FOV.Degrees = _owner._renderSettings.FOV.Get();
            viewFrustum.Write.NearPlane = _owner._renderSettings.NearPlane.Get();
            viewFrustum.Write.FarPlane = _owner._renderSettings.FarPlane.Get();

            var e = new Entity(entity, store);
            var cameraEntity = new CameraEntity(e, viewFrustum.Read, transform);

            View = cameraEntity.GetView();
            Projection = Matrix4x4.CreatePerspectiveFieldOfView(viewFrustum.Read.FOV.Radians, _owner._windowAspectRatio, viewFrustum.Read.NearPlane, viewFrustum.Read.FarPlane);

            _owner.MainCamera.Set(cameraEntity);
        }
    }

    private readonly struct ReadRenderableEntityAction(in GLRenderContext owner, in Frustum? frustum) : IForEach<TransformComponent, MeshRendererComponent>
    {
        private readonly GLRenderContext _owner = owner;
        private readonly Frustum? _frustum = frustum;

        public void Execute(float delta, DataStore store, int entity, in TransformComponent transform, in MeshRendererComponent meshRendererComponent)
        {
            MeshRenderer? meshRenderer = meshRendererComponent.MeshRenderer;
            if (meshRenderer == null || meshRenderer.IsDisposed)
            {
                return;
            }

            if (!meshRenderer.IsBound)
            {
                _owner._unboundRenderers.Enqueue(new UnboundRenderer(store.GetUuid(entity), meshRenderer));
            }

            Matrix4x4 matrix = transform.ToMatrix4X4();

            if (_frustum.HasValue)
            {
                BoundingBox bounds = meshRenderer.Mesh.Bounds;
                Vector3 worldCenter = Vector3.Transform(bounds.Center, matrix);
                Vector3 worldExtents = new(
                    MathF.Abs(matrix.M11) * bounds.Extents.X + MathF.Abs(matrix.M21) * bounds.Extents.Y + MathF.Abs(matrix.M31) * bounds.Extents.Z,
                    MathF.Abs(matrix.M12) * bounds.Extents.X + MathF.Abs(matrix.M22) * bounds.Extents.Y + MathF.Abs(matrix.M32) * bounds.Extents.Z,
                    MathF.Abs(matrix.M13) * bounds.Extents.X + MathF.Abs(matrix.M23) * bounds.Extents.Y + MathF.Abs(matrix.M33) * bounds.Extents.Z
                );

                if (!_frustum.Value.Intersects(worldCenter, worldExtents))
                {
                    return;
                }
            }

            _owner._renderableEntities.Add(new EntityModel(store.GetUuid(entity), matrix, meshRenderer));
        }
    }
}
