using System.Numerics;

namespace Swordfish.Audio;

/// <summary>Computes 3D stereo panning, distance attenuation, and Doppler pitch shift.</summary>
internal static class SpatialAudioSolver
{
    private const float EPSILON = 1e-5f;
    private const float MIN_DISTANCE_FLOOR = 1e-4f;
    private const float CENTER_PAN = 0.5f;
    private const float MAX_RADIAL_SPEED_RATIO = 0.95f;
    private const float MIN_PITCH_FLOOR = 0.01f;

    /// <summary>Solves spatial gain, stereo pan, and Doppler pitch multiplier for one source relative to the listener.</summary>
    public static SpatialAudioResult Solve(
        in SpatialListenerState listener,
        in Vector3 sourcePosition,
        in Vector3 sourceVelocity,
        in SpatialAudio spatial,
        in SpatialAudioConfig config
    ) {
        float spatialBlend = Math.Clamp(spatial.SpatialBlend, 0f, 1f);
        if (spatialBlend <= 0f)
        {
            return new SpatialAudioResult(VolumeMultiplier: 1f, Pan: CENTER_PAN, PitchMultiplier: 1f);
        }

        Vector3 offset = sourcePosition - listener.Position;
        float distance = offset.Length();
        Vector3 directionToSource = distance > EPSILON ? offset / distance : Vector3.Zero;

        float attenuation = ComputeAttenuation(
            distance,
            spatial.MinDistance,
            spatial.MaxDistance,
            spatial.RolloffFactor,
            spatial.RolloffMode
        );

        float pan = distance > EPSILON
            ? ComputePan(directionToSource, listener.Right)
            : CENTER_PAN;

        float dopplerPitch = distance > EPSILON
            ? ComputeDopplerPitch(directionToSource, listener.Velocity, sourceVelocity, spatial.DopplerFactor, in config)
            : 1f;

        float volumeMultiplier = 1f + (attenuation - 1f) * spatialBlend;
        float blendedPan = CENTER_PAN + (pan - CENTER_PAN) * spatialBlend;
        float blendedPitch = 1f + (dopplerPitch - 1f) * spatialBlend;

        return new SpatialAudioResult(volumeMultiplier, blendedPan, blendedPitch);
    }

    /// <summary>Computes distance attenuation in the range [0, 1] for the given distance and rolloff parameters.</summary>
    public static float ComputeAttenuation(
        float distance,
        float minDistance,
        float maxDistance,
        float rolloffFactor,
        AudioRolloffMode mode
    ) {
        float minDist = MathF.Max(minDistance, MIN_DISTANCE_FLOOR);
        if (distance <= minDist)
        {
            return 1f;
        }

        bool bounded = maxDistance > minDist;
        if (bounded && distance >= maxDistance)
        {
            return 0f;
        }

        float rolloff = MathF.Max(0f, rolloffFactor);
        if (rolloff <= 0f)
        {
            return 1f;
        }

        switch (mode)
        {
            case AudioRolloffMode.Linear:
                if (!bounded)
                {
                    return 0f;
                }

                return Math.Clamp(1f - rolloff * ((distance - minDist) / (maxDistance - minDist)), 0f, 1f);

            case AudioRolloffMode.Exponential:
            {
                float raw = MathF.Pow(distance / minDist, -rolloff);
                if (!bounded)
                {
                    return Math.Clamp(raw, 0f, 1f);
                }

                float endGain = MathF.Pow(maxDistance / minDist, -rolloff);
                return NormalizeBoundedGain(raw, endGain);
            }

            case AudioRolloffMode.Inverse:
            default:
            {
                float raw = minDist / (minDist + rolloff * (distance - minDist));
                if (!bounded)
                {
                    return Math.Clamp(raw, 0f, 1f);
                }

                float endGain = minDist / (minDist + rolloff * (maxDistance - minDist));
                return NormalizeBoundedGain(raw, endGain);
            }
        }
    }

    /// <summary>Computes stereo pan in [0, 1] where 0 is full left, 0.5 is center, and 1 is full right.</summary>
    public static float ComputePan(Vector3 directionToSource, Vector3 listenerRight)
    {
        float rightProjection = Vector3.Dot(directionToSource, listenerRight);
        return Math.Clamp(CENTER_PAN + CENTER_PAN * rightProjection, 0f, 1f);
    }

    /// <summary>Computes a clamped Doppler pitch multiplier from radial listener and source velocities.</summary>
    public static float ComputeDopplerPitch(
        Vector3 directionToSource,
        Vector3 listenerVelocity,
        Vector3 sourceVelocity,
        float dopplerFactor,
        in SpatialAudioConfig config
    ) {
        float effectiveDoppler = MathF.Max(0f, dopplerFactor) * MathF.Max(0f, config.DopplerScale);
        if (effectiveDoppler <= 0f || config.SpeedOfSound <= EPSILON)
        {
            return 1f;
        }

        float minPitch = MathF.Max(MIN_PITCH_FLOOR, config.MinDopplerPitch);
        float maxPitch = MathF.Max(minPitch, config.MaxDopplerPitch);
        float maxDelta = config.SpeedOfSound * MAX_RADIAL_SPEED_RATIO;

        float vListener = Math.Clamp(Vector3.Dot(listenerVelocity, directionToSource) * effectiveDoppler, -maxDelta, maxDelta);
        float vSource = Math.Clamp(Vector3.Dot(sourceVelocity, directionToSource) * effectiveDoppler, -maxDelta, maxDelta);

        float ratio = (config.SpeedOfSound + vListener) / (config.SpeedOfSound + vSource);
        return Math.Clamp(ratio, minPitch, maxPitch);
    }

    private static float NormalizeBoundedGain(float raw, float endGain)
    {
        if (endGain >= 1f - EPSILON)
        {
            return 0f;
        }

        return Math.Clamp((raw - endGain) / (1f - endGain), 0f, 1f);
    }
}
