using JoltPhysicsSharp;

namespace Swordfish.Physics.Jolt;

internal interface IJoltPhysics
{
    PhysicsSystem System { get; }

    /// <summary>Draws physics bodies into the debug renderer synchronized with the solver step.</summary>
    void DrawBodies(in DrawSettings settings, DebugRenderer renderer, BodyDrawFilter? filter = null);
}