namespace WaywardBeyond.Client.Core.UI;

internal readonly struct Notification(string text, NotificationType type = NotificationType.Toast)
{
    public readonly string Text = text;
    public readonly NotificationType Type = type;
    
    public static implicit operator Notification(string str) => new(str);
    public static implicit operator string(Notification notification) => notification.Text;
}