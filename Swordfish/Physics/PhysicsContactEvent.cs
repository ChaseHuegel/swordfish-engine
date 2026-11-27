using System.Numerics;

namespace Swordfish.Physics;

/// <summary>Describes a contact between two physics entities resolved during a solver step.</summary>
public readonly record struct PhysicsContactEvent(
    int EntityA,
    int EntityB,
    Vector3 Normal,
    Vector3 ContactPoint,
    Vector3 RelativeVelocity,
    float ImpactSpeed,
    PhysicsContactKind Kind
)
{
    /// <summary>Gets the entity index of the first body.</summary>
    public int EntityA { get; init; } = EntityA;

    /// <summary>Gets the entity index of the second body.</summary>
    public int EntityB { get; init; } = EntityB;

    /// <summary>Gets the world-space contact normal pointing from <see cref="EntityA"/> toward <see cref="EntityB"/>.</summary>
    public Vector3 Normal { get; init; } = Normal;

    /// <summary>Gets the world-space contact point on the manifold.</summary>
    public Vector3 ContactPoint { get; init; } = ContactPoint;

    /// <summary>Gets the pre-impulse velocity of <see cref="EntityA"/> relative to <see cref="EntityB"/> at <see cref="ContactPoint"/>.</summary>
    public Vector3 RelativeVelocity { get; init; } = RelativeVelocity;

    /// <summary>Gets the non-negative closing speed along <see cref="Normal"/> before impulse resolution.</summary>
    public float ImpactSpeed { get; init; } = ImpactSpeed;

    /// <summary>Gets the contact lifecycle phase.</summary>
    public PhysicsContactKind Kind { get; init; } = Kind;
}
