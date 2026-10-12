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
    public const float BASE_SPEED = 10f;
    public const float DECELERATION = 2f;
    public const float ANGULAR_DECELERATION = 10f;

    public static readonly Vector3 PlayerScale = Vector3.One;
    public static readonly Vector3 DefaultSpawnPosition = new(0f, 1f, 5f);

    public const float PLAYER_STANDING_HEIGHT = 1.8f;
    public const float PLAYER_FLYING_HEIGHT = 0.75f;
    public const float PLAYER_FLYING_OFFSET = -0.2f;
    public const float PLAYER_FLYING_EYE_OFFSET = -0.5f;

    /// <summary>Constructs a <see cref="PhysicsComponent"/> suitible for characters.</summary>
    /// <returns></returns>
    public static PhysicsComponent CreateCharacterPhysicsComponent()
    {
        return new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous);
    }

    /// <summary>Constructs a <see cref="ColliderComponent"/> suitible for characters.</summary>
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