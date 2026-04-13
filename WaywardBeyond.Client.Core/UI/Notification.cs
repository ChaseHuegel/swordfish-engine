namespace WaywardBeyond.Client.Core.UI;

internal readonly struct Notification
{
    public readonly NotificationType Type;
    public readonly string ID;
    public readonly string Text;
    public readonly float Amount;

    public Notification(string text, NotificationType type = NotificationType.Toast)
    {
        ID = text;
        Text = text;
        Type = type;
    }

    public Notification(string id, string text, float amount)
    {
        ID = id;
        Text = text;
        Amount = amount;
        Type = NotificationType.Bar;
    }

    public static implicit operator Notification(string str) => new(str);
    public static implicit operator string(Notification notification) => notification.Text;
}