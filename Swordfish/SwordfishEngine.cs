namespace Swordfish;

public static class SwordfishEngine
{
    public static Version Version => _version ??= typeof(SwordfishEngine).Assembly.GetName().Version!;
    private static Version? _version;

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
