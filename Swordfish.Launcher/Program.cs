namespace Swordfish.Launcher;

internal static class Program
{
    private static int Main(string[] args)
    {
        var engine = new SwordfishEngine(args);

        int exitCode = engine.Run();

        return exitCode;
    }
}