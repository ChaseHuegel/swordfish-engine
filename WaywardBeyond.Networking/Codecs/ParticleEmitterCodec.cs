using System;
using System.Numerics;
using Krill;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>Replicates <see cref="ParticleEmitterComponent"/> as <see cref="ParticleEmitterMessage"/>.</summary>
public sealed class ParticleEmitterCodec : IPayloadCodec<ParticleEmitterComponent>
{
    /// <summary>Maximum wire character length accepted for an emitter or burst effect identifier.</summary>
    public const int MAX_EFFECT_ID_LENGTH = 128;

    /// <inheritdoc/>
    public Result<byte[]> Serialize(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out ParticleEmitterComponent emitter))
        {
            return Result<byte[]>.FromFailure($"entity {entity} has no {nameof(ParticleEmitterComponent)} to serialize.");
        }

        string? effectId = ResolveEffectId(in emitter);
        if (!IsValid(effectId, emitter.RateScale, in emitter.Offset, in emitter.LocalDirection, out string error))
        {
            return Result<byte[]>.FromFailure($"entity {entity} has invalid {nameof(ParticleEmitterComponent)}: {error}");
        }

        return Result<byte[]>.FromSuccess(SerializeNormalized(in emitter, effectId!));
    }

    /// <inheritdoc/>
    public Result Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        ParticleEmitterMessage message;
        try
        {
            message = ParticleEmitterMessage.Deserialize(payload);
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }

        var offset = new Vector3(message.OffsetX, message.OffsetY, message.OffsetZ);
        var localDirection = new Vector3(message.LocalDirectionX, message.LocalDirectionY, message.LocalDirectionZ);
        if (!IsValid(message.EffectId, message.RateScale, in offset, in localDirection, out string error))
        {
            return Result.FromFailure($"entity {entity} has invalid {nameof(ParticleEmitterComponent)}: {error}");
        }

        if (store.TryGet(entity, out ParticleEmitterComponent current)
            && string.Equals(current.EffectId, message.EffectId, StringComparison.Ordinal)
            && current.Playing == message.Playing
            && current.RateScale == message.RateScale
            && current.TintRgba == message.TintRgba
            && current.Offset == offset
            && current.LocalDirection == localDirection)
        {
            return Result.FromSuccess();
        }

        store.AddOrUpdate(entity, new ParticleEmitterComponent(
            message.EffectId,
            message.Playing,
            message.RateScale,
            message.TintRgba
        )
        {
            Offset = offset,
            LocalDirection = localDirection,
        });

        return Result.FromSuccess();
    }

    /// <inheritdoc/>
    public byte[] Serialize(in ParticleEmitterComponent value)
    {
        return SerializeNormalized(in value, ResolveEffectId(in value) ?? string.Empty);
    }

    /// <summary>Returns the canonical string effect ID carried by <paramref name="emitter"/>.</summary>
    public static string? ResolveEffectId(in ParticleEmitterComponent emitter)
    {
        return !string.IsNullOrEmpty(emitter.EffectId) ? emitter.EffectId : emitter.Effect?.ID;
    }

    /// <summary>Validates wire-replicated fields of a particle emitter.</summary>
    public static bool IsValid(
        string? effectId,
        float rateScale,
        in Vector3 offset,
        in Vector3 localDirection,
        out string error
    ) {
        if (string.IsNullOrWhiteSpace(effectId))
        {
            error = "EffectId is required.";
            return false;
        }

        if (effectId.Length > MAX_EFFECT_ID_LENGTH)
        {
            error = $"EffectId exceeds {MAX_EFFECT_ID_LENGTH} characters.";
            return false;
        }

        if (!float.IsFinite(rateScale) || rateScale < 0f)
        {
            error = "RateScale must be finite and non-negative.";
            return false;
        }

        if (!IsFinite(in offset))
        {
            error = "Offset contains non-finite values.";
            return false;
        }

        if (!IsFinite(in localDirection))
        {
            error = "LocalDirection contains non-finite values.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static byte[] SerializeNormalized(in ParticleEmitterComponent value, string effectId)
    {
        var message = new ParticleEmitterMessage
        {
            EffectId = effectId,
            Playing = value.Playing,
            RateScale = value.RateScale,
            OffsetX = value.Offset.X,
            OffsetY = value.Offset.Y,
            OffsetZ = value.Offset.Z,
            LocalDirectionX = value.LocalDirection.X,
            LocalDirectionY = value.LocalDirection.Y,
            LocalDirectionZ = value.LocalDirection.Z,
            TintRgba = value.TintRgba,
        };

        return message.Serialize();
    }

    private static bool IsFinite(in Vector3 vector)
    {
        return float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);
    }
}
