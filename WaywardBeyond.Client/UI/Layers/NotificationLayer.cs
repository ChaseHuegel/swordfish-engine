using System;
using System.Collections.Generic;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Base UI layer for rendering active notifications of a specific type.</summary>
internal abstract class NotificationLayer(in NotificationService notificationService) : IUILayer
{
    private readonly NotificationService _notificationService = notificationService;

    /// <summary>Gets the notification type rendered by this layer.</summary>
    public abstract NotificationType Type { get; }

    /// <summary>Gets whether notifications in this layer draw a framed background pill.</summary>
    protected abstract bool HasBackground { get; }

    /// <summary>Gets whether only the newest matching notification is rendered.</summary>
    protected abstract bool OnlyRenderLatest { get; }

    /// <inheritdoc/>
    public abstract bool IsVisible();

    /// <inheritdoc/>
    public virtual Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        DateTime now = DateTime.Now;
        IReadOnlyList<NotificationState> activeStates = _notificationService.GetActiveNotifications();

        for (var i = 0; i < activeStates.Count; i++)
        {
            NotificationState state = activeStates[i];
            if (state.Notification.Type != Type)
            {
                continue;
            }

            state.Render(ui, now, HasBackground);

            if (OnlyRenderLatest)
            {
                break;
            }
        }

        return Result.FromSuccess();
    }
}
