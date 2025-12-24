namespace WaywardBeyond.Client.Core.UI;

internal struct Notification(string text)
{
    public string Text = text;
    
    public static implicit operator Notification(string str) => new(str);
    public static implicit operator string(Notification notification) => notification.Text;
}