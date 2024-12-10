using System.Numerics;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Bricks;

internal sealed class BrickEntityBuilder(
    in Shader shader,
    in TextureArray textureArray,
    in IFileParseService fileParseService,
    in DataStore dataStore)
{
    private readonly TextureArray _textureArray = textureArray;
    
    private readonly DataStore _dataStore = dataStore;
    
    private readonly BrickGridBuilder _brickGridBuilder = new(fileParseService, textureArray);
    
    private readonly Material _opaqueMaterial = new(shader, textureArray);
    
    private readonly Material _transparentMaterial = new(shader, textureArray)
    {
        Transparent = true,
    };

    private readonly RenderOptions _renderOptions = new()
    {
        DoubleFaced = false,
        Wireframe = false,
    };

    public Entity Create(string name, BrickGrid grid, Vector3 position, Quaternion orientation, Vector3 scale)
    {
        Vector3[] brickLocations = _brickGridBuilder.CreateCollisionData(grid);
        var brickRotations = new Quaternion[brickLocations.Length];
        var brickShapes = new Shape[brickLocations.Length];
        for (var i = 0; i < brickLocations.Length; i++)
        {
            brickShapes[i] = new Box3(Vector3.One);
            brickRotations[i] = Quaternion.Identity;
        }

        var transform = new TransformComponent(position, orientation, scale);

        int ptr = _dataStore.Alloc(new IdentifierComponent(name, "bricks"));
        int transparencyPtr = _dataStore.Alloc(new IdentifierComponent($"{name} [Transparency]", "bricks"));

        Mesh mesh = _brickGridBuilder.CreateMesh(grid);
        var renderer = new MeshRenderer(mesh, _opaqueMaterial, _renderOptions);
        _dataStore.AddOrUpdate(ptr, transform);
        _dataStore.AddOrUpdate(ptr, new MeshRendererComponent(renderer));
        _dataStore.AddOrUpdate(ptr, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        _dataStore.AddOrUpdate(ptr, new ColliderComponent(new CompoundShape(brickShapes, brickLocations, brickRotations)));
        _dataStore.AddOrUpdate(ptr, new BrickComponent(grid, transparencyPtr));
        
        mesh = _brickGridBuilder.CreateMesh(grid, true);
        renderer = new MeshRenderer(mesh, _transparentMaterial, _renderOptions);
        _dataStore.AddOrUpdate(transparencyPtr, transform);
        _dataStore.AddOrUpdate(transparencyPtr, new MeshRendererComponent(renderer));
        _dataStore.AddOrUpdate(transparencyPtr, new ChildComponent(ptr));
        
        return new Entity(ptr, _dataStore);
    }
}