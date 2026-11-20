using Shoal.CrashHandling;
using Swordfish.Library.IO;

namespace Swordfish.Launcher;

internal static class Program
{
    private static int Main(string[] args)
    {
        AppPaths appPaths = SwordfishEngine.ResolveAppPaths(args);
        var options = new CrashHandlerOptions
        {
            AppName = "Swordfish",
            AppVersion = SwordfishEngine.Version.ToString(),
            Headless = false,
            AppPaths = appPaths,
        };

        return CrashSupervisor.Run(args, options, appArgs =>
        {
            var engine = new SwordfishEngine(appArgs, appPaths);
            return engine.Run();
        });
    }
}
