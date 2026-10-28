using System.Numerics;
using Swordfish.Library.Types;
using Swordfish.Settings;

namespace Swordfish.Graphics;

public interface IWindowContext
{
    DataBinding<double> UpdateDelta { get; }
    DataBinding<double> RenderDelta { get; }

    Vector2 Resolution { get; }
    Vector2 MonitorResolution { get; }

    Action? Loaded { get; set; }
    Action? Closed { get; set; }
    Action<double>? Render { get; set; }
    Action<double>? Update { get; set; }
    Action? Focused { get; set; }
    Action? Unfocused { get; set; }
    Action<Vector2>? Resized { get; set; }

    Vector2 GetSize();

    /// <summary>Gets the distinct fullscreen resolutions supported by the current monitor.</summary>
    IReadOnlyList<Vector2> GetVideoModes();

    /// <summary>Applies a display mode, border, and window size as one window update.</summary>
    /// <param name="mode">The window mode to apply.</param>
    /// <param name="borderless">Whether the window suppresses its border.</param>
    /// <param name="width">The windowed width, or null to keep the saved windowed width.</param>
    /// <param name="height">The windowed height, or null to keep the saved windowed height.</param>
    void ApplyDisplaySettings(WindowMode mode, bool borderless, int? width = null, int? height = null);

    void Close();
    void SetIcon(Texture icon);
}