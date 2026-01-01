using System.Reflection;
using Swordfish.Library.Types;
using Version = WaywardBeyond.Client.Core.Saves.Version;

namespace WaywardBeyond.Client.Core;

internal static class WaywardBeyond
{
    public static readonly Version Version = new(_DataVersion: 1, _Name: AssemblyVersion, _Environment: "Development");

    public static DataBinding<GameState> GameState { get; } = new(Core.GameState.MainMenu);
    
    private static string AssemblyVersion => _assemblyVersion ??= typeof(WaywardBeyond).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "VERSION UNKNOWN";
    private static string? _assemblyVersion;

    public static bool IsPlaying()
    {
        return GameState == Core.GameState.Playing;
    }

    public static bool Pause()
    {
        if (GameState != Core.GameState.Playing)
        {
            return false;
        }

        GameState.Set(Core.GameState.Paused);
        return true;
    }
    
    public static bool Unpause()
    {
        if (GameState != Core.GameState.Paused)
        {
            return false;
        }

        GameState.Set(Core.GameState.Playing);
        return true;
    }
}