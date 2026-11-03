using System;
using System.Collections.Generic;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders top-center progress bar notifications such as skill XP telemetry.</summary>
internal sealed class Bars(in NotificationService notificationService)
{
    private readonly BarsView _view = new();

    private readonly NotificationService _notificationService = notificationService;
    private readonly HashSet<string> _seenNotificationIDs = [];
    private readonly List<NotificationBarState> _bars = [];

    /// <summary>Returns whether the progress bars container is visible.</summary>
    public bool IsVisible() => true;

    /// <summary>Renders active bar notifications deduplicated by notification ID.</summary>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        IReadOnlyList<NotificationState> activeStates = _notificationService.GetActiveNotifications();
        if (activeStates.Count == 0)
        {
            return Result.FromSuccess();
        }

        _seenNotificationIDs.Clear();
        _bars.Clear();
        DateTime now = DateTime.Now;

        for (var i = 0; i < activeStates.Count; i++)
        {
            NotificationState state = activeStates[i];
            if (state.Notification.Type != NotificationType.Bar)
            {
                continue;
            }

            if (!_seenNotificationIDs.Add(state.Notification.ID))
            {
                continue;
            }

            Notification notification = state.Notification;
            _bars.Add(new NotificationBarState(
                notification.Text,
                notification.Subtext ?? string.Empty,
                !string.IsNullOrEmpty(notification.Subtext),
                notification.Amount,
                notification.Segments ?? 0,
                state.GetAlpha(now),
                notification.Color
            ));
        }

        if (_bars.Count > 0)
        {
            _view.Render(ui, new BarsState(_bars));
        }

        return Result.FromSuccess();
    }
}
