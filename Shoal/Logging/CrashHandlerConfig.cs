namespace Shoal.Logging;

/// <summary>
///     Crash handler and crash dump settings loaded from the [CrashHandler] table in logging.toml.
/// </summary>
public sealed class CrashHandlerConfig
{
    [TomlProperty("Enabled")]
    public bool Enabled { get; set; } = true;

    [TomlProperty("MaxCrashReports")]
    public int MaxCrashReports { get; set; } = 20;

    [TomlProperty("AlertTailLines")]
    public int AlertTailLines { get; set; } = 80;

    [TomlProperty("AlertTailMaxChars")]
    public int AlertTailMaxChars { get; set; } = 16384;

    [TomlProperty("CollectDumps")]
    public bool CollectDumps { get; set; } = true;

    [TomlProperty("DumpType")]
    public CrashDumpType DumpType { get; set; } = CrashDumpType.Mini;

    [TomlProperty("MaxCrashDumps")]
    public int MaxCrashDumps { get; set; } = 5;

    [TomlProperty("MaxUploadBytes")]
    public int MaxUploadBytes { get; set; } = 8 * 1024 * 1024;

    internal void Normalize()
    {
        if (MaxCrashReports < 0)
        {
            MaxCrashReports = 0;
        }

        if (AlertTailLines < 1)
        {
            AlertTailLines = 1;
        }

        if (AlertTailMaxChars < 512)
        {
            AlertTailMaxChars = 512;
        }

        if (!Enum.IsDefined(DumpType))
        {
            DumpType = CrashDumpType.Mini;
        }

        if (MaxCrashDumps < 0)
        {
            MaxCrashDumps = 0;
        }

        if (MaxUploadBytes < 0)
        {
            MaxUploadBytes = 0;
        }
    }
}
