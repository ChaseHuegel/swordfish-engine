using System.Reflection;
using Swordfish.Library.Types;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client;

internal static class WaywardBeyond
{
    public static readonly Version Version = new(_DataVersion: SaveVersion.CurrentDataVersion, _Name: AssemblyVersion, _Environment: "Development");

    public static DataBinding<GameState> GameState { get; } = new(global::WaywardBeyond.Client.GameState.MainMenu);
    
    private static string AssemblyVersion => _assemblyVersion ??= typeof(WaywardBeyond).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "VERSION UNKNOWN";
    private static string? _assemblyVersion;

    public static bool IsPlaying()
    {
        return GameState == global::WaywardBeyond.Client.GameState.Playing;
    }
    
    public static bool IsInGame()
    {
        return GameState == global::WaywardBeyond.Client.GameState.Playing || GameState == global::WaywardBeyond.Client.GameState.Paused;
    }

    public static bool Pause()
    {
        if (GameState != global::WaywardBeyond.Client.GameState.Playing)
        {
            return false;
        }

        GameState.Set(global::WaywardBeyond.Client.GameState.Paused);
        return true;
    }
    
    public static bool Unpause()
    {
        if (GameState != global::WaywardBeyond.Client.GameState.Paused)
        {
            return false;
        }

        GameState.Set(global::WaywardBeyond.Client.GameState.Playing);
        return true;
    }
}