namespace Swordfish.Physics;

/// <summary>Identifies the lifecycle stage of a physics contact event.</summary>
public enum PhysicsContactKind
{
    /// <summary>A new contact manifold started between two bodies.</summary>
    Added = 0,

    /// <summary>An existing contact manifold remained active across a step.</summary>
    Persisted = 1,
}
