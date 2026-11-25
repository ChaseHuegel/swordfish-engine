using System.Numerics;

namespace Swordfish.Graphics.Particles;

/// <summary>Describes a one-shot burst of particles at a world-space position and direction.</summary>
public readonly record struct ParticleBurstRequest(
    ParticleEffect Effect,
    Vector3 Position,
    Vector3 Direction,
    int? Count = null,
    float SpeedScale = 1f,
    float SizeScale = 1f,
    uint TintRgba = 0u
);
