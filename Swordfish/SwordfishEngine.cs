using DryIoc;

namespace Swordfish;

public static class SwordfishEngine
{
    public static Version Version => _version ??= typeof(SwordfishEngine).Assembly.GetName().Version!;

    public static IContainer Container
    {
        get => _container!;
        internal set => _container = value;
    }

    private static Version? _version;
    private static IContainer? _container;

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
