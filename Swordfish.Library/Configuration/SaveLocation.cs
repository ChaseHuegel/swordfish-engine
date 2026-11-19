namespace Swordfish.Library.Configuration;

/// <summary>Controls where a registered config writes when saved.</summary>
public enum SaveLocation
{
    /// <summary>Saves under the application's user config directory.</summary>
    UserData,

    /// <summary>Saves under the install or working-directory config folder.</summary>
    Install,
}
