using System;
using System.Numerics;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.Extensions;

/// <summary>Rendering and lifetime helpers for <see cref="NotificationState"/>.</summary>
internal static class NotificationStateExtensions
{
    /// <summary>Renders a notification text element with its current lifetime fade.</summary>
    public static void Render(this in NotificationState state, UIBuilder<Material> ui, DateTime now, bool hasBackground)
    {
        float alpha = state.GetAlpha(now);
        var fade = new Vector4(1f, 1f, 1f, alpha);

        using (ui.Element())
        {
            ui.Passthrough = true;
            if (hasBackground)
            {
                ui.Padding = new Padding(left: 8, top: 3, right: 8, bottom: 3);
                ui.Color = UiTheme.ScrimSoft * fade;
            }

            using (ui.Text(state.Notification.Text, UiMetrics.SmallFont))
            {
                ui.Passthrough = true;
                ui.Color = UiTheme.Text * fade;
            }
        }
    }

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
