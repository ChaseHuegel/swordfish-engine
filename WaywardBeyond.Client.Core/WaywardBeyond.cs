using System;
using System.Reflection;

namespace WaywardBeyond.Client.Core;

internal static class WaywardBeyond
{
    public static string Version => _version ??= typeof(WaywardBeyond).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "VERSION UNKNOWN";
    private static string? _version;
}