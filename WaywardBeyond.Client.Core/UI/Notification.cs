namespace WaywardBeyond.Client.Core.UI;

internal readonly struct Notification
{
    public readonly NotificationType Type;
    public readonly string Text;
    public readonly float Amount;

    public Notification(string text, NotificationType type = NotificationType.Toast)
    {
        Text = text;
        Type = type;
    }

    public Notification(string text, float amount)
    {
        Text = text;
        Amount = amount;
        Type = NotificationType.Bar;
    }

    public static implicit operator Notification(string str) => new(str);
    public static implicit operator string(Notification notification) => notification.Text;
}