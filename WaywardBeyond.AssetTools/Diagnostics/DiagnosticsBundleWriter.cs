using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Validation;

namespace WaywardBeyond.AssetTools.Diagnostics;

/// <summary>
///     Creates a diagnostics zip bundle excluding content files and redacting paths outside workspace roots.
/// </summary>
public sealed partial class DiagnosticsBundleWriter
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true
    };

    /// <summary>
    ///     Writes <c>about.json</c>, <c>log-tail.txt</c>, <c>validation-summary.txt</c>, and <c>recent-errors.txt</c> to a zip archive.
    /// </summary>
    public Result WriteZip(string outputZipPath, in DiagnosticsBundleInput input, int maxLogTailLines = 200)
    {
        if (string.IsNullOrWhiteSpace(outputZipPath))
        {
            return Result.FromFailure("Output zip path is required.");
        }

        if (maxLogTailLines < 1)
        {
            return Result.FromFailure("Maximum log tail line count must be positive.");
        }

        string fullOutputPath = Path.GetFullPath(outputZipPath);
        string? outputDir = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrEmpty(outputDir))
        {
            return Result.FromFailure($"Cannot resolve directory for '{fullOutputPath}'.");
        }

        List<string> normalizedRoots = NormalizeWorkspaceRoots(input.WorkspaceRoots);
        string tempZip = Path.Combine(outputDir, $".diag-{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(outputDir);
            using (var fileStream = new FileStream(tempZip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                WriteEntry(archive, "about.json", BuildAboutJson(in input, normalizedRoots));
                WriteEntry(archive, "log-tail.txt", BuildLogTail(input.LogFilePath, normalizedRoots, maxLogTailLines));
                WriteEntry(archive, "validation-summary.txt", BuildValidationSummary(input.ValidationReport, normalizedRoots));
                WriteEntry(archive, "recent-errors.txt", BuildRecentErrors(input.RecentErrors, normalizedRoots));
            }

            File.Move(tempZip, fullOutputPath, overwrite: true);
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }

            return Result.FromFailure(ex);
        }
    }

    /// <summary>
    ///     Redacts any absolute filesystem path in <paramref name="text"/> that lies outside <paramref name="workspaceRoots"/>.
    /// </summary>
    public static string SanitizeText(string text, IReadOnlyList<string> workspaceRoots)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string redactedUnix = UnixAbsolutePathRegex().Replace(text, match => RedactIfOutsideWorkspace(match.Value, workspaceRoots));
        string redactedAll = WindowsAbsolutePathRegex().Replace(redactedUnix, match => RedactIfOutsideWorkspace(match.Value, workspaceRoots));
        return redactedAll;
    }

    private static string BuildAboutJson(in DiagnosticsBundleInput input, IReadOnlyList<string> normalizedRoots)
    {
        List<string> rootLabels = new(normalizedRoots.Count);
        for (int i = 0; i < normalizedRoots.Count; i++)
        {
            rootLabels.Add(Path.GetFileName(normalizedRoots[i].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        }

        var payload = new
        {
            toolVersion = input.ToolVersion,
            contentVersion = input.ContentVersion,
            workspaceMode = input.WorkspaceMode.ToString(),
            workspaceRoots = rootLabels,
            osDescription = RuntimeInformation.OSDescription,
            osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            glVendor = input.GlVendor,
            glRenderer = input.GlRenderer,
            avaloniaVersion = input.AvaloniaVersion,
            logFileName = string.IsNullOrWhiteSpace(input.LogFilePath) ? null : Path.GetFileName(input.LogFilePath),
            generatedUtc = DateTimeOffset.UtcNow.ToString("O")
        };

        return JsonSerializer.Serialize(payload, JSON_OPTIONS) + "\n";
    }

    private static string BuildLogTail(string? logFilePath, IReadOnlyList<string> normalizedRoots, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(logFilePath) || !File.Exists(logFilePath))
        {
            return "(no log file available)\n";
        }

        string[] lines = File.ReadAllLines(logFilePath);
        int start = Math.Max(0, lines.Length - maxLines);
        var builder = new StringBuilder();
        for (int i = start; i < lines.Length; i++)
        {
            builder.Append(SanitizeText(lines[i], normalizedRoots));
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static string BuildValidationSummary(ValidationReport? report, IReadOnlyList<string> normalizedRoots)
    {
        if (report == null)
        {
            return "No validation report available.\n";
        }

        var builder = new StringBuilder();
        builder.Append($"Fatal: {report.FatalCount}\n");
        builder.Append($"Broken: {report.BrokenCount}\n");
        builder.Append($"CrossPlatform: {report.CrossPlatformCount}\n");
        builder.Append($"Style: {report.StyleCount}\n");
        builder.Append($"Informational: {report.InformationalCount}\n\n");

        for (int i = 0; i < report.Findings.Count; i++)
        {
            ValidationFinding f = report.Findings[i];
            string safePath = ToRelativeOrRedacted(f.FilePath, normalizedRoots);
            string safeMessage = SanitizeText(f.Message, normalizedRoots);
            builder.Append($"[{f.Severity}] {f.Code} {safePath}:{f.Line} {safeMessage}\n");
        }

        return builder.ToString();
    }

    private static string BuildRecentErrors(IReadOnlyList<string>? errors, IReadOnlyList<string> normalizedRoots)
    {
        if (errors == null || errors.Count == 0)
        {
            return "(no recent errors)\n";
        }

        var builder = new StringBuilder();
        for (int i = 0; i < errors.Count; i++)
        {
            builder.Append(SanitizeText(errors[i], normalizedRoots));
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static List<string> NormalizeWorkspaceRoots(IReadOnlyList<string>? roots)
    {
        List<string> normalized = [];
        if (roots == null)
        {
            return normalized;
        }

        for (int i = 0; i < roots.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(roots[i]))
            {
                normalized.Add(Path.GetFullPath(roots[i]).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
        }

        return normalized;
    }

    private static string RedactIfOutsideWorkspace(string candidatePath, IReadOnlyList<string> normalizedRoots)
    {
        string normalized = candidatePath.Replace('\\', '/');
        for (int i = 0; i < normalizedRoots.Count; i++)
        {
            string root = normalizedRoots[i].Replace('\\', '/');
            if (string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase))
            {
                return "<workspace>";
            }

            string prefix = root + "/";
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return "<workspace>/" + normalized.Substring(prefix.Length);
            }
        }

        return "<redacted-path>";
    }

    private static string ToRelativeOrRedacted(string filePath, IReadOnlyList<string> normalizedRoots)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        return RedactIfOutsideWorkspace(Path.GetFullPath(filePath), normalizedRoots);
    }

    [GeneratedRegex(@"(?<![A-Za-z0-9_])/(?:[A-Za-z0-9._-]+/)+[A-Za-z0-9._-]+")]
    private static partial Regex UnixAbsolutePathRegex();

    [GeneratedRegex(@"[A-Za-z]:\\(?:[^\\\s""'<>|]+\\)*[^\\\s""'<>|]+")]
    private static partial Regex WindowsAbsolutePathRegex();
}
