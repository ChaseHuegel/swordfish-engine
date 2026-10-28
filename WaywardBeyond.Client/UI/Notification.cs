using System.Numerics;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.UI;

/// <summary>Represents an in-game notification payload.</summary>
internal readonly struct Notification
{
    public readonly NotificationType Type;
    public readonly string ID;
    public readonly string Text;
    public readonly string? Subtext;
    public readonly float Amount;
    public readonly int? Segments;
    public readonly Vector4? Color;

    /// <summary>Initializes a new text notification.</summary>
    public Notification(string text, NotificationType type = NotificationType.Toast)
    {
        ID = text;
        Text = text;
        Type = type;
    }

    /// <summary>Initializes a new progress bar notification.</summary>
    public Notification(
        string id,
        string text,
        float amount,
        string? subtext = null,
        int? segments = null,
        Vector4? color = null
    )
    {
        ID = id;
        Text = text;
        Subtext = subtext;
        Amount = amount;
        Segments = segments;
        Color = color;
        Type = NotificationType.Bar;
    }

    public static implicit operator Notification(string str) => new(str);
    public static implicit operator string(Notification notification) => notification.Text;
}
