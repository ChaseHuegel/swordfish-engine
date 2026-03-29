using System;
using System.Numerics;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core.Extensions;

internal static class NotificationStateExtensions
{
    public static void Render(this NotificationState state, UIBuilder<Material> ui, DateTime now, bool hasBackground)
    {
        using (ui.Text(state.Notification.Text))
        {
            TimeSpan elapsed = now - state.CreatedAt;
            float alpha = MathS.RangeToRange(
                input: (float)elapsed.TotalMilliseconds,
                low: 0,
                high: state.GetLifetime(),
                newLow: 0f,
                newHigh: 1f
            );
            
            //  Falloff near the end of the notification's lifetime
            //      Graph: https://www.desmos.com/calculator/udfsvtcbgn
            alpha = 1f - (float)Math.Pow(alpha, 9f);

            ui.Color = new Vector4(1f, 1f, 1f, alpha);

            if (hasBackground)
            {
                ui.BackgroundColor = new Vector4(0f, 0f, 0f, 0.75f * alpha);
            }
        }
    }

    public static int GetLifetime(this NotificationState state)
    {
        return state.Notification.Type switch
        {
            NotificationType.Interaction => 1500,
            _ => 3000,
        };
    }
}