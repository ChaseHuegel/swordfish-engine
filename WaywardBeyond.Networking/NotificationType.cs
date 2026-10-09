namespace WaywardBeyond.Networking;

/// <summary>
/// The display category a notification is rendered as. Carried byte-serialized on <see cref="NotificationMessage"/>
/// so servers - which never format text - can still direct the client's presentation (Toast, Action bar,
/// interaction prompt, or progress bar). The values are a wire contract; do not reorder.
/// </summary>
public enum NotificationType : byte
{
    Toast,
    Action,
    Interaction,
    Bar,
}