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
    private const int INNER_WIDTH = 488;
    private const int SEGMENT_GAP = 2;

    private readonly NotificationService _notificationService = notificationService;
    private readonly HashSet<string> _seenNotificationIDs = [];

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
        DateTime now = DateTime.Now;
        var bars = new List<NotificationBarState>();

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
            bars.Add(new NotificationBarState(
                notification.Text,
                notification.Subtext ?? string.Empty,
                !string.IsNullOrEmpty(notification.Subtext),
                notification.Amount,
                DividerPositions(notification.Segments),
                state.GetAlpha(now),
                notification.Color
            ));
        }

        if (bars.Count > 0)
        {
            new BarsView().Render(ui, new BarsState(bars));
        }

        return Result.FromSuccess();
    }

    private static List<int> DividerPositions(int? segmentCount)
    {
        var dividers = new List<int>();
        if (segmentCount is not > 1)
        {
            return dividers;
        }

        for (var segmentIndex = 1; segmentIndex < segmentCount.Value; segmentIndex++)
        {
            dividers.Add(INNER_WIDTH * segmentIndex / segmentCount.Value - SEGMENT_GAP / 2);
        }

        return dividers;
    }
}
