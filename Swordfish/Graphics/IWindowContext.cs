using System.Numerics;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
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

    /// <summary>Gets the display names of all connected monitors in enumeration order.</summary>
    IReadOnlyList<string> GetMonitors();

    /// <summary>Gets the distinct fullscreen resolutions supported by the specified monitor, or the current monitor when null or empty.</summary>
    IReadOnlyList<Vector2> GetVideoModes(string? monitorName = null);

    /// <summary>Applies a display mode, border, resolution, and preferred monitor as one window update.</summary>
    /// <param name="mode">The window mode to apply.</param>
    /// <param name="borderless">Whether the window suppresses its border.</param>
    /// <param name="width">The requested width, or null to keep the saved width for the target mode.</param>
    /// <param name="height">The requested height, or null to keep the saved height for the target mode.</param>
    /// <param name="monitorName">The preferred monitor name, empty string for automatic selection, or null to keep the saved monitor.</param>
    Result ApplyDisplaySettings(
        WindowMode mode,
        bool borderless,
        int? width = null,
        int? height = null,
        string? monitorName = null
    );

    void Close();
    void SetIcon(Texture icon);
}