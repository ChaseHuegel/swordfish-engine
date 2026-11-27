using System;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Generic completion and normalized progress math shared by all timed interactions.</summary>
public static class TimedInteractionProgress
{
    /// <summary>Tolerance absorbing accumulated frame-delta floating-point drift at hold completion.</summary>
    public const float COMPLETION_EPSILON = 1e-5f;

    /// <summary>Returns whether <paramref name="elapsed"/> has reached <paramref name="requiredDuration"/>.</summary>
    public static bool IsComplete(float elapsed, float requiredDuration)
    {
        if (!float.IsFinite(requiredDuration) || requiredDuration <= 0f)
        {
            return true;
        }

        if (!float.IsFinite(elapsed) || elapsed < 0f)
        {
            return false;
        }

        float tolerance = Math.Min(COMPLETION_EPSILON, requiredDuration * COMPLETION_EPSILON);
        return elapsed + tolerance >= requiredDuration;
    }

    /// <summary>Normalizes <paramref name="elapsed"/> against <paramref name="requiredDuration"/> into <c>[0..1]</c>.</summary>
    public static float Normalize(float elapsed, float requiredDuration)
    {
        if (!float.IsFinite(requiredDuration) || requiredDuration <= 0f)
        {
            return 1f;
        }

        if (!float.IsFinite(elapsed) || elapsed <= 0f)
        {
            return 0f;
        }

        return Math.Clamp(elapsed / requiredDuration, 0f, 1f);
    }
}
