using System;

namespace WaywardBeyond.Client.Core.UI;

internal struct NotificationState(in Notification notification, in DateTime createdAt)
{
    public readonly Notification Notification = notification;
    public readonly DateTime CreatedAt = createdAt;
}