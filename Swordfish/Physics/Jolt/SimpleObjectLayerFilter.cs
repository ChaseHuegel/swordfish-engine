using JoltPhysicsSharp;

namespace Swordfish.Physics.Jolt;

internal class SimpleObjectLayerFilter : ObjectLayerFilter
{
    protected override bool ShouldCollide(ObjectLayer layer)
    {
        return true;
    }
}
