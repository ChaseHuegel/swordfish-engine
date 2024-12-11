using System;

namespace WaywardBeyond.Client.Core;

internal static class WaywardBeyond
{
    public static Version Version => _version ??= typeof(WaywardBeyond).Assembly.GetName().Version!;
    private static Version? _version;
}