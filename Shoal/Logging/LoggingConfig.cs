using Tomlet;
using Tomlet.Models;

namespace Shoal.Logging;

/// <summary>
///     Logging and crash-handling configuration loaded from config/logging.toml.
/// </summary>
public sealed class LoggingConfig
{
    public const int MIN_FLUSH_INTERVAL_MS = 25;

    private const string FILE_NAME = "logging.toml";

    [TomlProperty("MinLevel")]
    public LogLevel MinLevel { get; set; } = LogLevel.Information;

    [TomlProperty("FlushIntervalMs")]
    public int FlushIntervalMs { get; set; } = 200;

    [TomlProperty("SyncFlushMinLevel")]
    public LogLevel SyncFlushMinLevel { get; set; } = LogLevel.Error;

    [TomlProperty("MaxLogFiles")]
    public int MaxLogFiles { get; set; } = 20;

    [TomlProperty("CrashHandler")]
    public CrashHandlerConfig CrashHandler { get; set; } = new();

    /// <summary>
    ///     Loads logging.toml from the configured application paths, falling back to shipped defaults.
    /// </summary>
    public static LoggingConfig Load(AppPaths appPaths)
    {
        string? path = ResolveConfigPath(appPaths);
        if (path == null)
        {
            return CreateDefault();
        }

        try
        {
            string text = File.ReadAllText(path);
            return Parse(text);
        }
        catch
        {
            return CreateDefault();
        }
    }

    /// <summary>
    ///     Loads logging.toml from the given base directory, falling back to defaults when missing or invalid.
    /// </summary>
    public static LoggingConfig Load(string? baseDirectory = null)
    {
        string root = string.IsNullOrWhiteSpace(baseDirectory) ? AppContext.BaseDirectory : baseDirectory;
        return Load(AppPaths.ForDirectory(new PathInfo(root)));
    }

    /// <summary>
    ///     Parses TOML text into a normalized <see cref="LoggingConfig"/>.
    /// </summary>
    public static LoggingConfig Parse(string toml)
    {
        var config = CreateDefault();
        if (string.IsNullOrWhiteSpace(toml))
        {
            return config;
        }

        TomlDocument document = new TomlParser().Parse(toml);

        if (TryGetString(document, "MinLevel", out string? minLevel) && Enum.TryParse(minLevel, ignoreCase: true, out LogLevel parsedMin))
        {
            config.MinLevel = parsedMin;
        }

        if (TryGetInt(document, "FlushIntervalMs", out int flushIntervalMs))
        {
            config.FlushIntervalMs = flushIntervalMs;
        }

        if (TryGetString(document, "SyncFlushMinLevel", out string? syncLevel) && Enum.TryParse(syncLevel, ignoreCase: true, out LogLevel parsedSync))
        {
            config.SyncFlushMinLevel = parsedSync;
        }

        if (TryGetInt(document, "MaxLogFiles", out int maxLogFiles))
        {
            config.MaxLogFiles = maxLogFiles;
        }

        if (document.ContainsKey("CrashHandler") && document.GetValue("CrashHandler") is TomlTable crashTable)
        {
            ParseCrashHandler(crashTable, config.CrashHandler);
        }

        config.Normalize();
        return config;
    }

    /// <summary>
    ///     Returns a new <see cref="LoggingConfig"/> populated with default values.
    /// </summary>
    public static LoggingConfig CreateDefault()
    {
        var config = new LoggingConfig();
        config.Normalize();
        return config;
    }

    internal void Normalize()
    {
        if (FlushIntervalMs < MIN_FLUSH_INTERVAL_MS)
        {
            FlushIntervalMs = MIN_FLUSH_INTERVAL_MS;
        }

        if (MaxLogFiles < 0)
        {
            MaxLogFiles = 0;
        }

        CrashHandler ??= new CrashHandlerConfig();
        CrashHandler.Normalize();
    }

    private static void ParseCrashHandler(TomlTable table, CrashHandlerConfig target)
    {
        if (TryGetBool(table, "Enabled", out bool enabled))
        {
            target.Enabled = enabled;
        }

        if (TryGetInt(table, "MaxCrashReports", out int maxCrashReports))
        {
            target.MaxCrashReports = maxCrashReports;
        }

        if (TryGetInt(table, "AlertTailLines", out int alertTailLines))
        {
            target.AlertTailLines = alertTailLines;
        }

        if (TryGetInt(table, "AlertTailMaxChars", out int alertTailMaxChars))
        {
            target.AlertTailMaxChars = alertTailMaxChars;
        }

        if (TryGetBool(table, "CollectDumps", out bool collectDumps))
        {
            target.CollectDumps = collectDumps;
        }

        if (TryGetString(table, "DumpType", out string? dumpType) && Enum.TryParse(dumpType, ignoreCase: true, out CrashDumpType parsedDumpType))
        {
            target.DumpType = parsedDumpType;
        }

        if (TryGetInt(table, "MaxCrashDumps", out int maxCrashDumps))
        {
            target.MaxCrashDumps = maxCrashDumps;
        }

        if (TryGetInt(table, "MaxUploadBytes", out int maxUploadBytes))
        {
            target.MaxUploadBytes = maxUploadBytes;
        }
    }

    private static string? ResolveConfigPath(AppPaths appPaths)
    {
        string userConfig = appPaths.ConfigRoot.At(FILE_NAME).Value;
        if (File.Exists(userConfig))
        {
            return userConfig;
        }

        string dataAssetsConfig = Path.Combine(appPaths.DataRoot.Value, "assets", "config", FILE_NAME);
        if (File.Exists(dataAssetsConfig))
        {
            return dataAssetsConfig;
        }

        string shippedPrimary = Path.Combine(AppContext.BaseDirectory, "config", FILE_NAME);
        if (File.Exists(shippedPrimary))
        {
            return shippedPrimary;
        }

        string shippedAssets = Path.Combine(AppContext.BaseDirectory, "assets", "config", FILE_NAME);
        if (File.Exists(shippedAssets))
        {
            return shippedAssets;
        }

        return null;
    }

    private static bool TryGetString(TomlTable table, string key, out string? value)
    {
        if (table.ContainsKey(key) && table.GetValue(key) is TomlString tomlString)
        {
            value = tomlString.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetInt(TomlTable table, string key, out int value)
    {
        if (table.ContainsKey(key) && table.GetValue(key) is TomlLong tomlLong)
        {
            value = (int)Math.Clamp(tomlLong.Value, int.MinValue, int.MaxValue);
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetBool(TomlTable table, string key, out bool value)
    {
        if (table.ContainsKey(key) && table.GetValue(key) is TomlBoolean tomlBool)
        {
            value = tomlBool.Value;
            return true;
        }

        value = false;
        return false;
    }
}
