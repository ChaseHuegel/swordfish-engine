using System.Text;
using System.Text.Json;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Packs;
using WaywardBeyond.AssetTools.Storage;
using WaywardBeyond.AssetTools.Validation;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Cli;

/// <summary>
///     Executes Asset Tools CLI commands writing results to stdout and diagnostics to stderr.
/// </summary>
public sealed class CliRunner
{
    /// <summary>
    ///     Schema version included in all <c>--json</c> CLI output documents.
    /// </summary>
    public const int JSON_SCHEMA_VERSION = 1;

    private const string TOOL_VERSION = "0.1.0";

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true
    };

    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;
    private readonly bool _useColor;

    /// <summary>
    ///     Creates a CLI runner writing structured output to <paramref name="stdout"/> and logs to <paramref name="stderr"/>.
    /// </summary>
    public CliRunner(TextWriter stdout, TextWriter stderr, bool useColor)
    {
        _stdout = stdout ?? throw new ArgumentNullException(nameof(stdout));
        _stderr = stderr ?? throw new ArgumentNullException(nameof(stderr));
        _useColor = useColor;
    }

    /// <summary>
    ///     Parses <paramref name="args"/>, executes the requested command, and returns a <see cref="CliExitCode"/> value.
    /// </summary>
    public int Run(string[] args)
    {
        Result<CliOptions> parsed = ParseArguments(args);
        if (!parsed.Success)
        {
            _stderr.WriteLine(parsed.Message);
            WriteUsage(_stderr);
            return CliExitCode.USAGE_OR_IO_ERROR;
        }

        CliOptions options = parsed.Value;
        return options.Command switch
        {
            "validate" => RunValidate(in options),
            "roundtrip" => RunRoundTrip(in options),
            "format" => RunFormat(in options),
            "new" => RunNew(in options),
            "ids" => RunIds(in options),
            _ => FailUsage($"Unknown command '{options.Command}'.")
        };
    }

    private int RunValidate(in CliOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TargetPath))
        {
            return FailUsage("validate requires a <root> directory.");
        }

        var loader = new WorkspaceLoader();
        Result<WorkspaceIndex> loaded = loader.LoadAuto(options.TargetPath, options.InstallPath);
        if (!loaded.Success)
        {
            _stderr.WriteLine(loaded.Message);
            return CliExitCode.USAGE_OR_IO_ERROR;
        }

        var validator = new WorkspaceValidator();
        ValidationReport report = validator.Validate(loaded.Value);

        if (options.Json)
        {
            var payload = new
            {
                schemaVersion = JSON_SCHEMA_VERSION,
                command = "validate",
                root = loaded.Value.RootPath,
                mode = loaded.Value.Mode.ToString(),
                blocksExport = report.BlocksExport,
                counts = new
                {
                    fatal = report.FatalCount,
                    broken = report.BrokenCount,
                    crossPlatform = report.CrossPlatformCount,
                    style = report.StyleCount,
                    informational = report.InformationalCount
                },
                findings = report.Findings.Select(static f => new
                {
                    code = f.Code,
                    severity = f.Severity.ToString(),
                    root = f.RootId,
                    file = f.FilePath,
                    line = f.Line,
                    column = f.Column,
                    field = f.Field,
                    message = f.Message,
                    suggestedFix = f.SuggestedFixValue
                })
            };
            _stdout.WriteLine(JsonSerializer.Serialize(payload, JSON_OPTIONS));
        }
        else
        {
            WriteValidationHumanOutput(report);
        }

        return report.BlocksExport ? CliExitCode.FINDINGS_OR_MISMATCH : CliExitCode.CLEAN;
    }

    private int RunRoundTrip(in CliOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TargetPath) || !Directory.Exists(options.TargetPath))
        {
            _stderr.WriteLine($"Directory does not exist: '{options.TargetPath}'.");
            return CliExitCode.USAGE_OR_IO_ERROR;
        }

        string fullRoot = Path.GetFullPath(options.TargetPath);
        List<string> files = EnumerateTomlFiles(fullRoot);
        List<object> failures = [];
        int checkedCount = 0;

        for (int i = 0; i < files.Count; i++)
        {
            string filePath = files[i];
            checkedCount++;
            byte[] originalBytes;
            try
            {
                originalBytes = File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                _stderr.WriteLine($"Failed reading '{filePath}': {ex.Message}");
                return CliExitCode.USAGE_OR_IO_ERROR;
            }

            TomlDocument doc = TomlDocument.Parse(originalBytes, filePath);
            byte[] emittedBytes = doc.ToBytes();
            bool bytesMatch = originalBytes.AsSpan().SequenceEqual(emittedBytes);

            if (doc.HasSyntaxErrors || !bytesMatch)
            {
                string reason = doc.HasSyntaxErrors
                    ? $"Syntax error: {(doc.Diagnostics.Count > 0 ? doc.Diagnostics[0].Message : "invalid TOML")}"
                    : "Emitted bytes differ from original input.";
                failures.Add(new { file = filePath, reason });
            }
        }

        bool clean = failures.Count == 0;
        if (options.Json)
        {
            var payload = new
            {
                schemaVersion = JSON_SCHEMA_VERSION,
                command = "roundtrip",
                root = fullRoot,
                checkOnly = options.CheckOnly,
                filesChecked = checkedCount,
                mismatchCount = failures.Count,
                mismatches = failures
            };
            _stdout.WriteLine(JsonSerializer.Serialize(payload, JSON_OPTIONS));
        }
        else
        {
            string status = clean
                ? Colorize("OK", "32")
                : Colorize("FAIL", "31");
            _stdout.WriteLine($"[{status}] Checked {checkedCount} TOML file(s), {failures.Count} mismatch(es).");
            for (int i = 0; i < failures.Count; i++)
            {
                _stdout.WriteLine($"  {failures[i]}");
            }
        }

        return clean ? CliExitCode.CLEAN : CliExitCode.FINDINGS_OR_MISMATCH;
    }

    private int RunFormat(in CliOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TargetPath) || !Directory.Exists(options.TargetPath))
        {
            _stderr.WriteLine($"Directory does not exist: '{options.TargetPath}'.");
            return CliExitCode.USAGE_OR_IO_ERROR;
        }

        string fullRoot = Path.GetFullPath(options.TargetPath);
        List<string> files = EnumerateTomlFiles(fullRoot);
        var saver = new AtomicFileSaver();
        List<string> formattedFiles = [];
        List<string> errorFiles = [];

        for (int i = 0; i < files.Count; i++)
        {
            string filePath = files[i];
            Result<TomlDocument> loaded = TomlDocument.Load(filePath);
            if (!loaded.Success)
            {
                _stderr.WriteLine(loaded.Message);
                return CliExitCode.USAGE_OR_IO_ERROR;
            }

            TomlDocument doc = loaded.Value;
            if (doc.HasSyntaxErrors)
            {
                errorFiles.Add(filePath);
                continue;
            }

            Result<string> formatted = TomlFormatter.Format(doc);
            if (!formatted.Success)
            {
                errorFiles.Add(filePath);
                continue;
            }

            byte[] originalBytes = doc.ToBytes();
            byte[] formattedBytes = Encoding.UTF8.GetBytes(formatted.Value);
            if (originalBytes.AsSpan().SequenceEqual(formattedBytes))
            {
                continue;
            }

            var req = new FileSaveRequest(filePath, formattedBytes, ContentHash.ComputeSha256Hex(originalBytes));
            AtomicSaveResult saved = saver.Save(in req);
            if (!saved.Success)
            {
                _stderr.WriteLine(saved.ErrorMessage);
                return CliExitCode.USAGE_OR_IO_ERROR;
            }

            formattedFiles.Add(filePath);
        }

        if (options.Json)
        {
            var payload = new
            {
                schemaVersion = JSON_SCHEMA_VERSION,
                command = "format",
                root = fullRoot,
                filesScanned = files.Count,
                formattedCount = formattedFiles.Count,
                errorCount = errorFiles.Count,
                formattedFiles,
                errorFiles
            };
            _stdout.WriteLine(JsonSerializer.Serialize(payload, JSON_OPTIONS));
        }
        else
        {
            _stdout.WriteLine($"Scanned {files.Count} TOML file(s): formatted {formattedFiles.Count}, syntax-error skipped {errorFiles.Count}.");
        }

        return errorFiles.Count > 0 ? CliExitCode.FINDINGS_OR_MISMATCH : CliExitCode.CLEAN;
    }

    private int RunNew(in CliOptions options)
    {
        if (!string.Equals(options.SubCommand, "pack", StringComparison.OrdinalIgnoreCase))
        {
            return FailUsage("Usage: new pack <dir> [--id <id>] [--namespace <ns>] [--name <name>] [--author <author>] [--json]");
        }

        if (string.IsNullOrWhiteSpace(options.TargetPath))
        {
            return FailUsage("new pack requires a <dir> path.");
        }

        string fullDir = Path.GetFullPath(options.TargetPath);
        string folderSlug = SanitizeSlug(Path.GetFileName(fullDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        string packId = string.IsNullOrWhiteSpace(options.PackId) ? folderSlug : options.PackId;
        string packNamespace = string.IsNullOrWhiteSpace(options.PackNamespace) ? folderSlug : options.PackNamespace;
        string packName = string.IsNullOrWhiteSpace(options.PackName) ? packId : options.PackName;
        string packAuthor = string.IsNullOrWhiteSpace(options.PackAuthor) ? "Unknown" : options.PackAuthor;

        var scaffolder = new PackScaffolder();
        Result<PackMetadata> created = scaffolder.CreatePack(fullDir, packId, packNamespace, packName, packAuthor, TOOL_VERSION);
        if (!created.Success)
        {
            _stderr.WriteLine(created.Message);
            return CliExitCode.USAGE_OR_IO_ERROR;
        }

        if (options.Json)
        {
            var payload = new
            {
                schemaVersion = JSON_SCHEMA_VERSION,
                command = "new pack",
                directory = fullDir,
                id = created.Value.Id,
                packNamespace = created.Value.Namespace,
                contentVersion = created.Value.ContentVersion,
                toolVersion = created.Value.ToolVersion
            };
            _stdout.WriteLine(JsonSerializer.Serialize(payload, JSON_OPTIONS));
        }
        else
        {
            _stdout.WriteLine($"Created content pack '{created.Value.Id}' (ContentVersion {created.Value.ContentVersion}) at '{fullDir}'.");
        }

        return CliExitCode.CLEAN;
    }

    private int RunIds(in CliOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TargetPath))
        {
            return FailUsage("ids requires a <root> directory.");
        }

        var loader = new WorkspaceLoader();
        Result<WorkspaceIndex> loaded = loader.LoadAuto(options.TargetPath, options.InstallPath);
        if (!loaded.Success)
        {
            _stderr.WriteLine(loaded.Message);
            return CliExitCode.USAGE_OR_IO_ERROR;
        }

        WorkspaceIndex workspace = loaded.Value;
        AssetKind[] listedKinds =
        [
            AssetKind.Brick,
            AssetKind.Item,
            AssetKind.Paint,
            AssetKind.Recipe,
            AssetKind.SlotGroup,
            AssetKind.Bar,
            AssetKind.Buff,
            AssetKind.Skill,
            AssetKind.Body,
            AssetKind.Material
        ];

        if (options.Json)
        {
            Dictionary<string, object> byKind = new(StringComparer.Ordinal);
            for (int i = 0; i < listedKinds.Length; i++)
            {
                AssetKind kind = listedKinds[i];
                IReadOnlyList<IndexedAsset> assets = workspace.GetWinningAssetsByKind(kind);
                byKind[kind.ToString()] = assets.Select(static a => new
                {
                    id = a.Id,
                    root = a.Provenance.ProviderRootId,
                    isOverride = a.Provenance.IsOverride,
                    file = a.Provenance.RelativePath
                });
            }

            var payload = new
            {
                schemaVersion = JSON_SCHEMA_VERSION,
                command = "ids",
                root = workspace.RootPath,
                mode = workspace.Mode.ToString(),
                assets = byKind
            };
            _stdout.WriteLine(JsonSerializer.Serialize(payload, JSON_OPTIONS));
        }
        else
        {
            for (int i = 0; i < listedKinds.Length; i++)
            {
                AssetKind kind = listedKinds[i];
                IReadOnlyList<IndexedAsset> assets = workspace.GetWinningAssetsByKind(kind);
                _stdout.WriteLine($"[{kind}] ({assets.Count})");
                for (int j = 0; j < assets.Count; j++)
                {
                    IndexedAsset asset = assets[j];
                    string overrideBadge = asset.Provenance.IsOverride ? " [override]" : string.Empty;
                    _stdout.WriteLine($"  {asset.Id} ({asset.Provenance.ProviderRootId}){overrideBadge}");
                }
            }
        }

        return CliExitCode.CLEAN;
    }

    private void WriteValidationHumanOutput(ValidationReport report)
    {
        string headline = report.BlocksExport ? Colorize("BLOCKED", "31") : Colorize("OK", "32");
        _stdout.WriteLine($"[{headline}] {report.FatalCount} fatal, {report.BrokenCount} broken, {report.CrossPlatformCount} cross-platform, {report.StyleCount} style, {report.InformationalCount} informational");

        for (int i = 0; i < report.Findings.Count; i++)
        {
            ValidationFinding f = report.Findings[i];
            string sevTag = ColorizeSeverity(f.Severity);
            _stdout.WriteLine($"  {sevTag} {f.Code} {f.FilePath}:{f.Line}:{f.Column} ({f.Field}) {f.Message}");
        }
    }

    private string ColorizeSeverity(ValidationSeverity severity)
    {
        string label = $"[{severity}]";
        return severity switch
        {
            ValidationSeverity.Fatal => Colorize(label, "31"),
            ValidationSeverity.Broken => Colorize(label, "91"),
            ValidationSeverity.CrossPlatform => Colorize(label, "33"),
            ValidationSeverity.Style => Colorize(label, "36"),
            _ => Colorize(label, "90")
        };
    }

    private string Colorize(string text, string ansiCode)
    {
        return _useColor ? $"\u001b[{ansiCode}m{text}\u001b[0m" : text;
    }

    private int FailUsage(string message)
    {
        _stderr.WriteLine(message);
        WriteUsage(_stderr);
        return CliExitCode.USAGE_OR_IO_ERROR;
    }

    private static Result<CliOptions> ParseArguments(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return Result<CliOptions>.FromFailure("No command specified.");
        }

        string command = args[0].Trim().ToLowerInvariant();
        string? subCommand = null;
        string? targetPath = null;
        bool json = false;
        bool checkOnly = false;
        string? installPath = null;
        string? packId = null;
        string? packNamespace = null;
        string? packName = null;
        string? packAuthor = null;

        List<string> positionals = [];
        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
            }
            else if (string.Equals(arg, "--check", StringComparison.OrdinalIgnoreCase))
            {
                checkOnly = true;
            }
            else if (string.Equals(arg, "--install", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    return Result<CliOptions>.FromFailure("--install requires a directory path.");
                }

                installPath = args[++i];
            }
            else if (string.Equals(arg, "--id", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    return Result<CliOptions>.FromFailure("--id requires a value.");
                }

                packId = args[++i];
            }
            else if (string.Equals(arg, "--namespace", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    return Result<CliOptions>.FromFailure("--namespace requires a value.");
                }

                packNamespace = args[++i];
            }
            else if (string.Equals(arg, "--name", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    return Result<CliOptions>.FromFailure("--name requires a value.");
                }

                packName = args[++i];
            }
            else if (string.Equals(arg, "--author", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    return Result<CliOptions>.FromFailure("--author requires a value.");
                }

                packAuthor = args[++i];
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                return Result<CliOptions>.FromFailure($"Unknown option '{arg}'.");
            }
            else
            {
                positionals.Add(arg);
            }
        }

        if (string.Equals(command, "new", StringComparison.Ordinal))
        {
            if (positionals.Count < 2)
            {
                return Result<CliOptions>.FromFailure("new requires 'pack <dir>'.");
            }

            subCommand = positionals[0];
            targetPath = positionals[1];
        }
        else if (positionals.Count > 0)
        {
            targetPath = positionals[0];
        }

        return Result<CliOptions>.FromSuccess(new CliOptions(
            command,
            subCommand,
            targetPath,
            json,
            checkOnly,
            installPath,
            packId,
            packNamespace,
            packName,
            packAuthor));
    }

    private static List<string> EnumerateTomlFiles(string rootDirectory)
    {
        List<string> result = [];
        Stack<string> pending = new();
        pending.Push(rootDirectory);

        while (pending.Count > 0)
        {
            string current = pending.Pop();
            foreach (string subDir in Directory.EnumerateDirectories(current))
            {
                string name = Path.GetFileName(subDir);
                if (string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "BenchmarkDotNet.Artifacts", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                pending.Push(subDir);
            }

            foreach (string file in Directory.EnumerateFiles(current, "*.toml"))
            {
                result.Add(Path.GetFullPath(file));
            }
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static string SanitizeSlug(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_')
            {
                builder.Append(c);
            }
            else if (c == ' ' || c == '.')
            {
                builder.Append('_');
            }
        }

        return builder.Length > 0 ? builder.ToString() : "content_pack";
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  validate <root> [--install <dir>] [--json]");
        writer.WriteLine("  roundtrip <root> [--check] [--json]");
        writer.WriteLine("  format <root> [--json]");
        writer.WriteLine("  new pack <dir> [--id <id>] [--namespace <ns>] [--name <name>] [--author <author>] [--json]");
        writer.WriteLine("  ids <root> [--install <dir>] [--json]");
    }
}
