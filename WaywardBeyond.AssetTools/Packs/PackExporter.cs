using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Storage;
using WaywardBeyond.AssetTools.Validation;
using WaywardBeyond.AssetTools.Workspace;
using GameContentVersion = WaywardBeyond.Data.ContentVersion;

namespace WaywardBeyond.AssetTools.Packs;

/// <summary>
///     Validates, stages, stamps <c>pack.toml</c>, and archives a content pack without modifying the working tree.
/// </summary>
public sealed class PackExporter
{
    private readonly WorkspaceValidator _validator;
    private readonly AtomicFileSaver _atomicSaver;

    /// <summary>
    ///     Creates a pack exporter backed by <paramref name="validator"/> and <paramref name="atomicSaver"/>.
    /// </summary>
    public PackExporter(WorkspaceValidator? validator = null, AtomicFileSaver? atomicSaver = null)
    {
        _validator = validator ?? new WorkspaceValidator();
        _atomicSaver = atomicSaver ?? new AtomicFileSaver();
    }

    /// <summary>
    ///     Validates <paramref name="workspace"/> and exports a staged archive into <paramref name="outputDirectory"/>.
    /// </summary>
    public PackExportOutcome Export(
        WorkspaceIndex workspace,
        string outputDirectory,
        string toolVersion,
        bool confirmWarnings = false,
        PackArchiveFormat format = PackArchiveFormat.Auto)
    {
        if (workspace == null)
        {
            return new PackExportOutcome(false, null, false, false, null, "Workspace is required.");
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return new PackExportOutcome(false, null, false, false, null, "Output directory is required.");
        }

        Result<WorkspaceRoot> primaryRootResult = workspace.GetPrimaryWritableRoot();
        if (!primaryRootResult.Success)
        {
            return new PackExportOutcome(false, null, false, false, null, primaryRootResult.Message);
        }

        WorkspaceRoot packRoot = primaryRootResult.Value;
        ValidationReport report = _validator.Validate(workspace);
        if (report.BlocksExport)
        {
            return new PackExportOutcome(
                false,
                null,
                BlockedByValidation: true,
                RequiresWarningConfirmation: false,
                report,
                $"Export blocked by {report.FatalCount} fatal and {report.BrokenCount} broken validation finding(s).");
        }

        if (report.HasWarnings && !confirmWarnings)
        {
            return new PackExportOutcome(
                false,
                null,
                BlockedByValidation: false,
                RequiresWarningConfirmation: true,
                report,
                $"Export has {report.CrossPlatformCount} cross-platform and {report.StyleCount} style warning(s); confirmation required.");
        }

        Result<PackMetadata> existingMeta = PackMetadata.Load(packRoot.RootDirectory);
        string packId = existingMeta.Success ? existingMeta.Value.Id : SanitizeArtifactSegment(packRoot.Id);
        string packNamespace = existingMeta.Success ? existingMeta.Value.Namespace : SanitizeArtifactSegment(packRoot.Id);
        DateTimeOffset createdAt = existingMeta.Success ? existingMeta.Value.CreatedAt : DateTimeOffset.UtcNow;

        var stampedMetadata = new PackMetadata(
            packId,
            packNamespace,
            GameContentVersion.CURRENT,
            string.IsNullOrWhiteSpace(toolVersion) ? "0.1.0" : toolVersion.Trim(),
            createdAt);

        PackArchiveFormat resolvedFormat = format == PackArchiveFormat.Auto
            ? PackArchiveFormat.Zip
            : format;

        string extension = resolvedFormat == PackArchiveFormat.Zip ? ".zip" : ".tar.gz";
        string artifactFileName = $"{SanitizeArtifactSegment(packId)}-{GameContentVersion.CURRENT}{extension}";
        string fullOutputDir = Path.GetFullPath(outputDirectory);
        string archivePath = Path.Combine(fullOutputDir, artifactFileName);
        string stagingDir = Path.Combine(Path.GetTempPath(), $"wb-pack-stage-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(fullOutputDir);
            Directory.CreateDirectory(stagingDir);

            CopyPackTreeToStage(packRoot.RootDirectory, stagingDir);

            string stagedPackToml = Path.Combine(stagingDir, PackMetadata.FILE_NAME);
            var metaReq = new FileSaveRequest(stagedPackToml, Encoding.UTF8.GetBytes(stampedMetadata.ToToml()));
            AtomicSaveResult metaSave = _atomicSaver.Save(in metaReq);
            if (!metaSave.Success)
            {
                return new PackExportOutcome(false, null, false, false, report, metaSave.ErrorMessage);
            }

            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            if (resolvedFormat == PackArchiveFormat.Zip)
            {
                ZipFile.CreateFromDirectory(stagingDir, archivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
            }
            else
            {
                WriteTarGzArchive(stagingDir, archivePath);
            }

            return new PackExportOutcome(true, archivePath, false, false, report, null);
        }
        catch (Exception ex)
        {
            return new PackExportOutcome(false, null, false, false, report, ex.Message);
        }
        finally
        {
            CleanupStagingDirectory(stagingDir);
        }
    }

    private static void CopyPackTreeToStage(string sourceRoot, string stagingRoot)
    {
        string manifestSource = Path.Combine(sourceRoot, "manifest.toml");
        if (File.Exists(manifestSource))
        {
            File.Copy(manifestSource, Path.Combine(stagingRoot, "manifest.toml"), overwrite: true);
        }

        string assetsSource = Path.Combine(sourceRoot, "assets");
        if (Directory.Exists(assetsSource))
        {
            CopyDirectoryRecursive(assetsSource, Path.Combine(stagingRoot, "assets"));
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        string[] files = Directory.GetFiles(sourceDir);
        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileName(files[i]);
            if (fileName.StartsWith('.'))
            {
                continue;
            }

            File.Copy(files[i], Path.Combine(destinationDir, fileName), overwrite: true);
        }

        string[] subDirs = Directory.GetDirectories(sourceDir);
        for (int i = 0; i < subDirs.Length; i++)
        {
            string dirName = Path.GetFileName(subDirs[i]);
            if (dirName.StartsWith('.') ||
                string.Equals(dirName, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dirName, "obj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CopyDirectoryRecursive(subDirs[i], Path.Combine(destinationDir, dirName));
        }
    }

    private static void WriteTarGzArchive(string stagingDirectory, string archivePath)
    {
        using var fileStream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var gzip = new GZipStream(fileStream, CompressionLevel.Optimal);
        TarFile.CreateFromDirectory(stagingDirectory, gzip, includeBaseDirectory: false);
    }

    private static string SanitizeArtifactSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool ok = (c >= 'a' && c <= 'z') ||
                      (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9') ||
                      c == '-' ||
                      c == '_' ||
                      c == '.';
            builder.Append(ok ? c : '-');
        }

        return builder.Length > 0 ? builder.ToString() : "pack";
    }

    private static void CleanupStagingDirectory(string stagingDir)
    {
        try
        {
            if (Directory.Exists(stagingDir))
            {
                Directory.Delete(stagingDir, recursive: true);
            }
        }
        catch (IOException ex)
        {
            Trace.TraceWarning($"Failed cleaning up pack export stage '{stagingDir}': {ex.Message}");
        }
    }
}
