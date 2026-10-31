using System;
using System.Collections.Generic;
using Reef;
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
        var actionText = string.Empty;
        var actionAlpha = 0f;
        IReadOnlyList<NotificationState> activeStates = _notificationService.GetActiveNotifications();
        for (var i = 0; i < activeStates.Count; i++)
        {
            NotificationState state = activeStates[i];
            if (state.Notification.Type != NotificationType.Action)
            {
                continue;
            }

            actionText = state.Notification.Text;
            actionAlpha = state.GetAlpha(DateTime.Now);
            break;
        }

        var indicators = new List<IndicatorState>(_actionIndicators.Length);
        for (var i = 0; i < _actionIndicators.Length; i++)
        {
            IActionIndicator indicator = _actionIndicators[i];
            if (indicator.IsVisible() && indicator.IndicatorIcon != null)
            {
                indicators.Add(new IndicatorState(indicator.IndicatorIcon));
            }
        }

        new ActionsView().Render(
            ui,
            new ActionsState(actionText, actionAlpha, actionText.Length > 0, indicators)
        );

        return Result.FromSuccess();
    }
}
