using JoltPhysicsSharp;

namespace Swordfish.Physics.Jolt;

/// <summary>A reusable Jolt body filter that excludes one body id.</summary>
internal sealed class IgnoreBodyFilter : BodyFilter
{
    public BodyID IgnoredBodyID;

    /// <inheritdoc/>
    protected override bool ShouldCollide(BodyID bodyID)
    {
        return bodyID != IgnoredBodyID;
    }

    /// <inheritdoc/>
    protected override bool ShouldCollideLocked(Body body)
    {
        return body.ID != IgnoredBodyID;
    }
}
