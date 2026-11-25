using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Util;

namespace Swordfish.IO;

/// <summary>TOML asset schema for a particle effect loaded by <see cref="ParticleEffectDatabase"/>.</summary>
internal struct ParticleEffectDefinition()
{
    public string? Texture;
    public ParticleBlendMode BlendMode = ParticleBlendMode.Alpha;
    public float Rate = 0f;
    public int BurstCount = 1;
    public int MaxParticles = 128;
    public float LifetimeMin = 0.5f;
    public float LifetimeMax = 0.5f;
    public float SpeedMin = 1f;
    public float SpeedMax = 1f;
    public float SpreadDegrees = 15f;
    public float SpawnRadius = 0f;
    public float StartSizeMin = 0.1f;
    public float StartSizeMax = 0.1f;
    public float EndSizeMin = 0.1f;
    public float EndSizeMax = 0.1f;
    public string StartColor = "#ffffff";
    public string? SecondaryStartColor;
    public string EndColor = "#ffffff";
    public float StartAlpha = 1f;
    public float EndAlpha = 0f;
    public float EmissiveIntensity = 0f;
    public float Drag = 0f;
    public Float3 Gravity;
    public float InitialRotationMin = 0f;
    public float InitialRotationMax = 0f;
    public float AngularVelocityMin = 0f;
    public float AngularVelocityMax = 0f;

    /// <summary>Validates this definition and builds a runtime <see cref="ParticleEffect"/>.</summary>
    public readonly Result<ParticleEffect> Build(string id, Texture? texture)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<ParticleEffect>.FromFailure("Particle effect ID must not be empty.");
        }

        if (!float.IsFinite(Rate) || Rate < 0f)
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" Rate must be finite and >= 0, got {Rate}.");
        }

        if (BurstCount < 0)
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" BurstCount must be >= 0, got {BurstCount}.");
        }

        if (MaxParticles < 1)
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" MaxParticles must be >= 1, got {MaxParticles}.");
        }

        if (!IsValidPositiveRange(LifetimeMin, LifetimeMax, allowZeroMin: false))
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" LifetimeMin/Max must be finite, > 0, and Min <= Max.");
        }

        if (!IsValidPositiveRange(SpeedMin, SpeedMax, allowZeroMin: true))
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" SpeedMin/Max must be finite, >= 0, and Min <= Max.");
        }

        if (!float.IsFinite(SpreadDegrees) || SpreadDegrees < 0f || SpreadDegrees > 180f)
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" SpreadDegrees must be within [0, 180], got {SpreadDegrees}.");
        }

        if (!float.IsFinite(SpawnRadius) || SpawnRadius < 0f)
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" SpawnRadius must be finite and >= 0, got {SpawnRadius}.");
        }

        if (!IsValidPositiveRange(StartSizeMin, StartSizeMax, allowZeroMin: false))
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" StartSizeMin/Max must be finite, > 0, and Min <= Max.");
        }

        if (!IsValidPositiveRange(EndSizeMin, EndSizeMax, allowZeroMin: true))
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" EndSizeMin/Max must be finite, >= 0, and Min <= Max.");
        }

        if (!float.IsFinite(StartAlpha) || StartAlpha < 0f || StartAlpha > 1f ||
            !float.IsFinite(EndAlpha) || EndAlpha < 0f || EndAlpha > 1f)
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" StartAlpha and EndAlpha must be within [0, 1].");
        }

        if (!float.IsFinite(EmissiveIntensity) || EmissiveIntensity < 0f)
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" EmissiveIntensity must be finite and >= 0, got {EmissiveIntensity}.");
        }

        if (!float.IsFinite(Drag) || Drag < 0f)
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" Drag must be finite and >= 0, got {Drag}.");
        }

        if (!Gravity.IsFinite)
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" Gravity must be finite.");
        }

        if (!IsValidRange(InitialRotationMin, InitialRotationMax))
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" InitialRotationMin/Max must be finite and Min <= Max.");
        }

        if (!IsValidRange(AngularVelocityMin, AngularVelocityMax))
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" AngularVelocityMin/Max must be finite and Min <= Max.");
        }

        if (!ParticleEffect.TryParseHexColor(StartColor, StartAlpha, out Vector4 startColor))
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" has invalid StartColor \"{StartColor}\".");
        }

        Vector4? secondaryStartColor = null;
        if (!string.IsNullOrWhiteSpace(SecondaryStartColor))
        {
            if (!ParticleEffect.TryParseHexColor(SecondaryStartColor, StartAlpha, out Vector4 parsedSecondary))
            {
                return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" has invalid SecondaryStartColor \"{SecondaryStartColor}\".");
            }

            secondaryStartColor = parsedSecondary;
        }

        if (!ParticleEffect.TryParseHexColor(EndColor, EndAlpha, out Vector4 endColor))
        {
            return Result<ParticleEffect>.FromFailure($"Particle effect \"{id}\" has invalid EndColor \"{EndColor}\".");
        }

        var effect = new ParticleEffect(
            id,
            texture,
            BlendMode,
            Rate,
            BurstCount,
            MaxParticles,
            new ParticleRange(LifetimeMin, LifetimeMax),
            new ParticleRange(SpeedMin, SpeedMax),
            SpreadDegrees,
            SpawnRadius,
            new ParticleRange(StartSizeMin, StartSizeMax),
            new ParticleRange(EndSizeMin, EndSizeMax),
            in startColor,
            in secondaryStartColor,
            in endColor,
            EmissiveIntensity,
            Gravity.ToVector3(),
            Drag,
            new ParticleRange(InitialRotationMin, InitialRotationMax),
            new ParticleRange(AngularVelocityMin, AngularVelocityMax)
        );
        return Result<ParticleEffect>.FromSuccess(effect);
    }

    private static bool IsValidPositiveRange(float min, float max, bool allowZeroMin)
    {
        if (!IsValidRange(min, max))
        {
            return false;
        }

        return allowZeroMin ? min >= 0f : min > 0f;
    }

    private static bool IsValidRange(float min, float max)
    {
        return float.IsFinite(min) && float.IsFinite(max) && max >= min;
    }

    /// <summary>A three-component float vector for particle effect TOML tables.</summary>
    public record struct Float3()
    {
        public float X;
        public float Y;
        public float Z;

        public Float3(float x, float y, float z) : this()
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>True when all components are finite numbers.</summary>
        public readonly bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);

        /// <summary>Converts this definition vector to <see cref="Vector3"/>.</summary>
        public readonly Vector3 ToVector3() => new(X, Y, Z);
    }
}
