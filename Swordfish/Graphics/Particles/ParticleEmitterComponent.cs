using System.Numerics;
using Krill;

namespace Swordfish.Graphics.Particles;

/// <summary>Emits continuous particles from an entity's world transform.</summary>
public struct ParticleEmitterComponent : IDataComponent
{
    /// <summary>Particle effect asset emitted by this component.</summary>
    public ParticleEffect? Effect;

    /// <summary>Asset ID resolved through the particle database when <see cref="Effect"/> is null.</summary>
    public string? EffectId;

    /// <summary>Whether emission is currently active.</summary>
    public bool Playing;

    /// <summary>Multiplier applied to the effect's base emission rate.</summary>
    public float RateScale;

    /// <summary>Local-space offset from the entity's position.</summary>
    public Vector3 Offset;

    /// <summary>Local-space emission axis rotated by the entity's orientation.</summary>
    public Vector3 LocalDirection;

    /// <summary>Packed <c>0xRRGGBBAA</c> sRGB color tint; <c>0</c> leaves effect colors untinted.</summary>
    public uint TintRgba;

    /// <summary>Creates an emitter component for <paramref name="effect"/>.</summary>
    public ParticleEmitterComponent(ParticleEffect effect, bool playing = true, float rateScale = 1f, uint tintRgba = 0u)
    {
        Effect = effect;
        EffectId = effect?.ID;
        Playing = playing;
        RateScale = rateScale;
        Offset = Vector3.Zero;
        LocalDirection = Vector3.UnitY;
        TintRgba = tintRgba;
    }

    /// <summary>Creates an emitter component referencing an effect asset ID for deferred or headless resolution.</summary>
    public ParticleEmitterComponent(string effectId, bool playing = true, float rateScale = 1f, uint tintRgba = 0u)
    {
        Effect = null;
        EffectId = effectId;
        Playing = playing;
        RateScale = rateScale;
        Offset = Vector3.Zero;
        LocalDirection = Vector3.UnitY;
        TintRgba = tintRgba;
    }
}
