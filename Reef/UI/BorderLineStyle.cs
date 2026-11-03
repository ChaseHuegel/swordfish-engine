namespace Reef.UI;

/// <summary>The stroke pattern shared by the painted sides of a border.</summary>
public enum BorderLineStyle : byte
{
    /// <summary>Draws a continuous line.</summary>
    Solid = 0,

    /// <summary>Draws dashes twice as long as the side thickness.</summary>
    Dashed = 1,

    /// <summary>Draws dashes as long as the side thickness.</summary>
    Dotted = 2,
}
