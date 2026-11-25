namespace WaywardBeyond.AssetTools.Settings;

/// <summary>
///     Preferred OpenGL context backend for the 3D preview viewport.
/// </summary>
public enum GlBackendPreference
{
    /// <summary>
    ///     Select the platform default backend automatically.
    /// </summary>
    Auto = 0,

    /// <summary>
    ///     Prefer native Windows OpenGL (WGL).
    /// </summary>
    Wgl = 1,

    /// <summary>
    ///     Prefer ANGLE OpenGL ES on Windows.
    /// </summary>
    Angle = 2,

    /// <summary>
    ///     Prefer Linux GLX.
    /// </summary>
    Glx = 3,

    /// <summary>
    ///     Prefer Linux EGL.
    /// </summary>
    Egl = 4,

    /// <summary>
    ///     Force software OpenGL rendering.
    /// </summary>
    Software = 5
}
