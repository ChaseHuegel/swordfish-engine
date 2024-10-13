using JoltPhysicsSharp;

namespace Swordfish.Physics.Jolt;

internal class SimpleBodyFilter : BodyFilter
{
    protected override bool ShouldCollide(BodyID bodyID)
    {
        return true;
    }

    protected override bool ShouldCollideLocked(Body body)
    {
        return true;
    }
}