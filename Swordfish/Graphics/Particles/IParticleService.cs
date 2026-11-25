using System.Numerics;
using Swordfish.Library.Util;

namespace Swordfish.Graphics.Particles;

/// <summary>Provides thread-safe one-shot particle burst emission and lifecycle control.</summary>
public interface IParticleService
{
    /// <summary>Number of currently live particles as of the latest simulation tick.</summary>
    int ActiveParticleCount { get; }

    /// <summary>Queues a one-shot particle burst for the next simulation tick.</summary>
    void EmitBurst(in ParticleBurstRequest request);

    /// <summary>Resolves <paramref name="effectId"/> and queues a one-shot burst for the next simulation tick.</summary>
    Result EmitBurst(string effectId, Vector3 position, Vector3 direction, int? count = null);

    /// <summary>Resolves <paramref name="effectId"/> and queues a scaled and tinted one-shot burst for the next simulation tick.</summary>
    Result EmitBurst(
        string effectId,
        Vector3 position,
        Vector3 direction,
        int? count,
        float speedScale,
        float sizeScale,
        uint tintRgba = 0u
    ) => EmitBurst(effectId, position, direction, count);

    /// <summary>Clears all active particles and pending bursts.</summary>
    void Clear();
}
