using JoltPhysicsSharp;

namespace Swordfish.Physics.Jolt;

internal class SimpleBroadPhaseLayerFilter : BroadPhaseLayerFilter
{
    protected override bool ShouldCollide(BroadPhaseLayer layer)
    {
        return true;
    }
}
