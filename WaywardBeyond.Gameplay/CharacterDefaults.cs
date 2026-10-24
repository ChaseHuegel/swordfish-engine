using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;

namespace WaywardBeyond.Gameplay;

//  TODO This class should be replaced by something data driven.
//       Body TOML definitions in particular should define offsets
//       and heights since it varies by the visual, similarly to view models of items
/// <summary>A utility for common character representation and movement values.</summary>
public static class CharacterDefaults
{
    //  TODO this is a little odd here
    /// <summary>The number of hotbar slots.</summary>
    public const int HOTBAR_SLOT_COUNT = 9;
    
    /// <summary>The default interaction reach from the origin.</summary>
    public const float DEFAULT_REACH = 9.5f;
    
    /// <summary>The base movement speed in meters per second.</summary>
    public const float BASE_SPEED = 10f;

    /// <summary>The per-second linear velocity damping factor.</summary>
    public const float DECELERATION = 2f;

    /// <summary>The per-second angular velocity damping factor.</summary>
    public const float ANGULAR_DECELERATION = 10f;

    /// <summary>A squared velocity at or below this magnitude snaps to rest.</summary>
    public const float STOP_EPSILON = 0.00001f;

    /// <summary>The character's scale.</summary>
    public static readonly Vector3 PlayerScale = Vector3.One;

    /// <summary>The standing character height.</summary>
    public const float PLAYER_STANDING_HEIGHT = 1.8f;

    /// <summary>The flying character height.</summary>
    public const float PLAYER_FLYING_HEIGHT = 0.75f;

    /// <summary>The flying character vertical offset from the body origin.</summary>
    public const float PLAYER_FLYING_OFFSET = -0.2f;

    /// <summary>The flying character's eye vertical offset from the body origin.</summary>
    public const float PLAYER_FLYING_EYE_OFFSET = -0.5f;

    /// <summary>Constructs a <see cref="PhysicsComponent"/> suitable for characters.</summary>
    public static PhysicsComponent CreateCharacterPhysicsComponent()
    {
        return new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous);
    }

    /// <summary>Constructs a <see cref="ColliderComponent"/> suitable for characters.</summary>
    public static ColliderComponent CreateCharacterColliderComponent(Vector3 scale)
    {
        var box = new Shape(new Box3(new Vector3(0.25f, PLAYER_FLYING_HEIGHT, 0.25f) * scale));
        var compound = new CompoundShape(
            [box],
            [new Vector3(0f, PLAYER_FLYING_OFFSET, 0f) * scale],
            [Quaternion.Identity]
        );
        return new ColliderComponent(compound);
    }
}