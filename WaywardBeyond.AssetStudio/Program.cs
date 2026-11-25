using Avalonia;
using Avalonia.X11;
using Shoal.CrashHandling;
using Shoal.IO;
using Swordfish.Library.IO;
using WaywardBeyond.AssetStudio.Services;
using WaywardBeyond.AssetTools.Localization;
using WaywardBeyond.AssetTools.Settings;

namespace WaywardBeyond.AssetStudio;

/// <summary>
///     Application bootstrapper for the Wayward Beyond Asset Studio desktop executable.
/// </summary>
internal static class Program
{
    private const string APP_NAME = "Wayward Beyond Asset Studio";
    private const string APP_VERSION = "0.1.0-alpha";

    /// <summary>
    ///     Entry point configuring crash supervision, file logging, localization, GL backend preference, and Avalonia.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        var paths = ToolDataPaths.Resolve();
        var settingsStore = new ToolSettingsStore(paths);
        var loadedSettings = settingsStore.Load();
        var settings = loadedSettings.Success ? loadedSettings.Value : new ToolSettings();

        ToolLocalization.ApplyCulture(settings.CultureOverride);

        var bypassSupervisor = ShouldBypassCrashSupervisor(args);
        if (bypassSupervisor)
        {
            return RunDesktopApp(args, paths, settings);
        }

        var crashOptions = new CrashHandlerOptions
        {
            AppName = APP_NAME,
            AppVersion = APP_VERSION,
            AppPaths = AppPaths.ForDirectory(new PathInfo(paths.DataDirectory)),
        };

        return CrashSupervisor.Run(args, crashOptions, forwardedArgs => RunDesktopApp(forwardedArgs, paths, settings));
    }

    /// <summary>
    ///     Constructs the configured Avalonia application builder with Inter font and platform GL backend order.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        var glPreference = AppStartupContext.Current?.GlPreference ?? GlBackendPreference.Auto;
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(BuildWin32Options(glPreference))
            .With(BuildX11Options(glPreference))
            .LogToTrace();
    }

    private static int RunDesktopApp(string[] args, ToolDataPaths paths, ToolSettings settings)
    {
        var logsDir = Path.Combine(paths.DataDirectory, "logs");
        using var logSession = new StudioLogSession(logsDir, settings.LogLevel);
        AppStartupContext.Current = new AppStartupContext
        {
            InitialWorkspacePath = ExtractWorkspacePathArg(args),
            DataPaths = paths,
            LogSession = logSession,
            GlPreference = settings.GlPreference,
        };

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            logSession.LogError("Unhandled exception in Asset Studio desktop host", ex);
            throw;
        }
    }

    private static bool ShouldBypassCrashSupervisor(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--no-crash-supervisor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[i], "--no-crash-handler", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ExtractWorkspacePathArg(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith('-'))
            {
                continue;
            }

            if (Directory.Exists(arg))
            {
                return Path.GetFullPath(arg);
            }
        }

        return null;
    }

    private static Win32PlatformOptions BuildWin32Options(GlBackendPreference preference)
    {
        IReadOnlyList<Win32RenderingMode> modes = preference switch
        {
            GlBackendPreference.Angle => [Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl, Win32RenderingMode.Software],
            GlBackendPreference.Software => [Win32RenderingMode.Software],
            _ => [Win32RenderingMode.Wgl, Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
        };

        return new Win32PlatformOptions
        {
            RenderingMode = modes,
        };
    }

    private static X11PlatformOptions BuildX11Options(GlBackendPreference preference)
    {
        IReadOnlyList<X11RenderingMode> modes = preference switch
        {
            GlBackendPreference.Egl => [X11RenderingMode.Egl, X11RenderingMode.Glx, X11RenderingMode.Software],
            GlBackendPreference.Software => [X11RenderingMode.Software],
            _ => [X11RenderingMode.Glx, X11RenderingMode.Egl, X11RenderingMode.Software],
        };

        return new X11PlatformOptions
        {
            RenderingMode = modes,
        };
    }
}
