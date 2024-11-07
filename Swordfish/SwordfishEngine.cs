using Swordfish.Library.Collections;

namespace Swordfish;

public static class SwordfishEngine
{
    public static Version Version => _version ??= typeof(SwordfishEngine).Assembly.GetName().Version!;

    public static Kernel Kernel
    {
        get => _kernel!;
        internal set => _kernel = value;
    }

    private static Version? _version;
    private static Kernel? _kernel;

    // ReSharper disable once UnusedMember.Global
    public static void Stop(int exitCode = 0)
    {
        Program.Stop(exitCode);
    }

    // ReSharper disable once UnusedMember.Global
    public static void Crash(int exitCode = (int)ExitCode.Crash)
    {
        Program.Crash(exitCode);
    }
}
