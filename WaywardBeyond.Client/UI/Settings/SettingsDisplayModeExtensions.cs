using Swordfish.Settings;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Maps display mode choices to window settings.</summary>
internal static class SettingsDisplayModeExtensions
{
    /// <summary>Creates a display mode choice from the current window settings.</summary>
    public static SettingsDisplayMode FromWindowSettings(WindowMode mode, bool borderless)
    {
        return mode switch
        {
            WindowMode.Windowed when borderless => SettingsDisplayMode.BorderlessWindow,
            WindowMode.Windowed => SettingsDisplayMode.Windowed,
            WindowMode.Maximized when borderless => SettingsDisplayMode.BorderlessMaximized,
            WindowMode.Maximized => SettingsDisplayMode.Maximized,
            WindowMode.Fullscreen when borderless => SettingsDisplayMode.BorderlessFullscreen,
            WindowMode.Fullscreen => SettingsDisplayMode.Fullscreen,
            _ => SettingsDisplayMode.Windowed,
        };
    }

    /// <summary>Gets the window mode for a display mode choice.</summary>
    public static WindowMode ToWindowMode(this SettingsDisplayMode mode)
    {
        return mode switch
        {
            SettingsDisplayMode.Maximized => WindowMode.Maximized,
            SettingsDisplayMode.BorderlessMaximized => WindowMode.Maximized,
            SettingsDisplayMode.Fullscreen => WindowMode.Fullscreen,
            SettingsDisplayMode.BorderlessFullscreen => WindowMode.Fullscreen,
            _ => WindowMode.Windowed,
        };
    }

    /// <summary>Gets whether the display mode choice uses the selected window size.</summary>
    public static bool UsesWindowSize(this SettingsDisplayMode mode)
    {
        return mode is SettingsDisplayMode.Windowed or SettingsDisplayMode.BorderlessWindow;
    }

    /// <summary>Gets whether the display mode choice hides the window border.</summary>
    public static bool IsBorderless(this SettingsDisplayMode mode)
    {
        return mode is SettingsDisplayMode.BorderlessWindow
            or SettingsDisplayMode.BorderlessMaximized
            or SettingsDisplayMode.BorderlessFullscreen;
    }
}
