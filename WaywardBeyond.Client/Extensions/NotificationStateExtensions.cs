using System;
using Swordfish.Library.Util;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.Extensions;

/// <summary>Lifetime helpers for <see cref="NotificationState"/>.</summary>
internal static class NotificationStateExtensions
{
    /// <summary>Computes the normalized opacity for a notification at the given timestamp.</summary>
    public static float GetAlpha(this in NotificationState state, DateTime now)
    {
        TimeSpan elapsed = now - state.CreatedAt;
        float progress = MathS.RangeToRange(
            input: (float)elapsed.TotalMilliseconds,
            low: 0,
            high: state.GetLifetime(),
            newLow: 0f,
            newHigh: 1f
        );
        progress = Math.Clamp(progress, 0f, 1f);

        //  Falloff near the end of the notification's lifetime
        //      Graph: https://www.desmos.com/calculator/udfsvtcbgn
        return 1f - (float)Math.Pow(progress, 9f);
    }

    /// <summary>Returns the display lifetime in milliseconds for the notification.</summary>
    public static int GetLifetime(this in NotificationState state)
    {
        return state.Notification.Type switch
        {
            NotificationType.Interaction => 1500,
            _ => 3000,
        };
    }
}
