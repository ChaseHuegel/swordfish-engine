using System.Text.Json;

namespace Shoal.CrashHandling;

/// <summary>
///     Runtime diagnostic metadata written by a supervised process and read by the crash supervisor on failure.
/// </summary>
public sealed class CrashSessionMetadata
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    public string? SessionLogPath { get; set; }
    public string? LatestLogPath { get; set; }
    public string? AppVersion { get; set; }
    public string? EngineState { get; set; }
    public string? GpuVendor { get; set; }
    public string? GpuRenderer { get; set; }
    public string? GpuVersion { get; set; }
    public List<string> LoadedModules { get; set; } = [];
    public string? FaultingThread { get; set; }
    public string? UnhandledException { get; set; }

    /// <summary>
    ///     Writes this metadata snapshot to disk.
    /// </summary>
    public void SaveTo(string path)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(this, _jsonOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            //  Metadata persistence must never disrupt app execution.
        }
    }

    /// <summary>
    ///     Reads a metadata snapshot from disk, returning an empty snapshot if unavailable.
    /// </summary>
    public static CrashSessionMetadata TryLoadFrom(string path)
    {
        if (!File.Exists(path))
        {
            return new CrashSessionMetadata();
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CrashSessionMetadata>(json, _jsonOptions) ?? new CrashSessionMetadata();
        }
        catch
        {
            return new CrashSessionMetadata();
        }
    }
}
