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
        Result result;

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

            DrawLatestAction(ui);
            result = DrawIndicators(ui, delta);
        }

        return result;
    }

    /// <summary>Draws the newest action notification in its own full-width row.</summary>
    private void DrawLatestAction(UIBuilder<Material> ui)
    {
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

                new NotificationView().Render(
                    ui,
                    new NotificationViewState(state.Notification.Text, state.GetAlpha(DateTime.Now), HasBackground: false)
                );
            }

            return;
        }
    }

    /// <summary>Draws the visible action indicators centered against the viewport, and reports their result.</summary>
    private Result DrawIndicators(UIBuilder<Material> ui, double delta)
    {
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
                if (!actionIndicator.IsVisible())
                {
                    continue;
                }

                Result result = actionIndicator.RenderIndicator(delta, ui);
                if (!result)
                {
                    return result;
                }
            }
        }

        return Result.FromSuccess();
    }
}
