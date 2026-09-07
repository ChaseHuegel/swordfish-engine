using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Shared player body and movement configuration. Both the client view builder and the server
/// authority builder construct identical physics bodies from this fragment, and the shared motion step
/// integrates against the same movement constants, so prediction and authority never diverge.
/// </summary>
public static class PlayerBodyConfig
{
    /// <summary>The fixed physics step (seconds) the shared motion step runs at, matching <c>JoltPhysicsSystem</c>.</summary>
    public const float PHYSICS_STEP = 0.016f;

    public const float BASE_SPEED = 10f;
    public const float DECELERATION = 2f;
    public const float JUMP_SPEED = 8f;
    public const float ANGULAR_DECELERATION = 10f;

    public static readonly Vector3 PLAYER_SCALE = Vector3.One;
    public static readonly Vector3 DEFAULT_SPAWN_POSITION = new(0f, 1f, 5f);

    private const float PLAYER_STANDING_HEIGHT = 1.7f;
    private const float PLAYER_STANDING_OFFSET = -0.75f;
    private const float PLAYER_FLYING_HEIGHT = 0.75f;
    private const float PLAYER_FLYING_OFFSET = -0.2f;

    public static PhysicsComponent CreatePhysics()
    {
        return new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous);
    }

    public static ColliderComponent CreateCollider(Vector3 scale)
    {
        var capsule = new Shape(new Box3(new Vector3(0.25f, PLAYER_FLYING_HEIGHT, 0.25f) * scale));
        var compound = new CompoundShape(
            [capsule],
            [new Vector3(0f, PLAYER_FLYING_OFFSET, 0f) * scale],
            [Quaternion.Identity]
        );
        return new ColliderComponent(compound);
    }
}