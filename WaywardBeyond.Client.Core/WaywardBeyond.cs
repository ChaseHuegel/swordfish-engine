using System.Reflection;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core;

internal static class WaywardBeyond
{
    public static readonly Version Version = new(_DataVersion: 1, _Name: AssemblyVersion, _Environment: "Development");
    
    public static GameState GameState { get; set; }
    
    private static string AssemblyVersion => _assemblyVersion ??= typeof(WaywardBeyond).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "VERSION UNKNOWN";
    private static string? _assemblyVersion;
}