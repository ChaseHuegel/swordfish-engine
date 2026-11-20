namespace Shoal.CrashHandling;

/// <summary>
///     Process-wide bridge between the running application and its parent crash supervisor.
/// </summary>
public static class CrashSession
{
    public const string ENV_SESSION_PREFIX = "SHOAL_CRASH_SESSION";
    public const string ENV_SUPERVISED_CONSOLE = "SHOAL_SUPERVISED_CONSOLE";

    private static readonly object _lock = new();
    private static readonly CrashSessionMetadata _metadata = new();
    private static string? _sessionPrefix = Environment.GetEnvironmentVariable(ENV_SESSION_PREFIX);

    /// <summary>
    ///     Gets whether this process was launched by a <see cref="CrashSupervisor"/>.
    /// </summary>
    public static bool IsSupervised => !string.IsNullOrWhiteSpace(_sessionPrefix);

    /// <summary>
    ///     Gets whether the supervisor already owns console output for this session.
    /// </summary>
    public static bool HasSupervisedConsole => Environment.GetEnvironmentVariable(ENV_SUPERVISED_CONSOLE) == "1";

    /// <summary>
    ///     Gets the shared ring-buffer path when supervised; otherwise null.
    /// </summary>
    public static string? RingBufferPath => IsSupervised ? _sessionPrefix + ".ring" : null;

    /// <summary>
    ///     Gets the session metadata file path when supervised; otherwise null.
    /// </summary>
    public static string? MetadataPath => IsSupervised ? _sessionPrefix + ".meta.json" : null;

    internal static void RefreshFromEnvironment()
    {
        lock (_lock)
        {
            _sessionPrefix = Environment.GetEnvironmentVariable(ENV_SESSION_PREFIX);
        }
    }

    /// <summary>
    ///     Records the active session log paths for the supervisor.
    /// </summary>
    public static void SetLogPaths(string timestampedLogPath, string latestLogPath)
    {
        Update(meta =>
        {
            meta.SessionLogPath = timestampedLogPath;
            meta.LatestLogPath = latestLogPath;
        });
    }

    /// <summary>
    ///     Records the current engine lifecycle state.
    /// </summary>
    public static void SetEngineState(string state)
    {
        Update(meta => meta.EngineState = state);
    }

    /// <summary>
    ///     Records the OpenGL/graphics device description once initialized.
    /// </summary>
    public static void SetGpuInfo(string? vendor, string? renderer, string? version)
    {
        Update(meta =>
        {
            meta.GpuVendor = vendor;
            meta.GpuRenderer = renderer;
            meta.GpuVersion = version;
        });
    }

    /// <summary>
    ///     Records a loaded Shoal module name and identifier.
    /// </summary>
    public static void AddLoadedModule(string moduleSummary)
    {
        if (string.IsNullOrWhiteSpace(moduleSummary))
        {
            return;
        }

        Update(meta =>
        {
            if (!meta.LoadedModules.Contains(moduleSummary, StringComparer.Ordinal))
            {
                meta.LoadedModules.Add(moduleSummary);
            }
        });
    }

    /// <summary>
    ///     Records an unhandled managed exception and faulting thread before termination.
    /// </summary>
    public static void RecordUnhandledException(string threadDescription, string exceptionText)
    {
        Update(meta =>
        {
            meta.FaultingThread = threadDescription;
            meta.UnhandledException = exceptionText;
        });
    }

    private static void Update(Action<CrashSessionMetadata> mutate)
    {
        lock (_lock)
        {
            mutate(_metadata);
            string? path = MetadataPath;
            if (path != null)
            {
                _metadata.SaveTo(path);
            }
        }
    }
}
