using System.Numerics;
using Krill;

namespace WaywardBeyond.Server.Particles;

/// <summary>Describes a server-authoritative one-shot particle burst to replicate to clients.</summary>
public readonly record struct ServerParticleBurst(
    string EffectId,
    Vector3 Position,
    Vector3 Direction,
    int? Count = null,
    float SpeedScale = 1f,
    float SizeScale = 1f,
    uint TintRgba = 0u,
    Uuid ExcludeClient = default
);
