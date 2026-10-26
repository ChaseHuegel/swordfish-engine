using System;
using System.Collections.Generic;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders the transient fading action notification and active indicator icons above the hotbar.</summary>
internal sealed class Actions(
    in IActionIndicator[] actionIndicators,
    in NotificationService notificationService
)
{
    private readonly IActionIndicator[] _actionIndicators = actionIndicators;
    private readonly NotificationService _notificationService = notificationService;

    /// <summary>Returns whether the actions strip is active.</summary>
    public bool IsVisible() => true;

    /// <summary>Renders the latest action notification and any visible action indicators.</summary>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.Spacing = 8;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(ui.Width),
            };

            //  Render the latest transient action notification in its own full-width row so text length
            //  changes never perturb the horizontal alignment of the indicator icons below.
            IReadOnlyList<NotificationState> activeStates = _notificationService.GetActiveNotifications();
            for (var i = 0; i < activeStates.Count; i++)
            {
                NotificationState state = activeStates[i];
                if (state.Notification.Type != NotificationType.Action)
                {
                    continue;
                }

                using (ui.Element())
                {
                    ui.Passthrough = true;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };

                    state.Render(ui, DateTime.Now, hasBackground: false);
                }

                break;
            }

            //  Render active indicator icons centered directly against the full viewport width
            using (ui.Element())
            {
                ui.Passthrough = true;
                ui.LayoutDirection = LayoutDirection.Horizontal;
                ui.Spacing = 12;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };

                for (var i = 0; i < _actionIndicators.Length; i++)
                {
                    IActionIndicator actionIndicator = _actionIndicators[i];
                    if (actionIndicator.IsVisible())
                    {
                        Result result = actionIndicator.RenderIndicator(delta, ui);
                        if (!result)
                        {
                            return result;
                        }
                    }
                }
            }
        }

        return Result.FromSuccess();
    }
}
