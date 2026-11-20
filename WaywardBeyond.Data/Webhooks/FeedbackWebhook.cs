using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Swordfish.Library.Extensions;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Webhooks;

/// <summary>
///     Posts player feedback and crash report archives to the configured feedback webhook.
/// </summary>
public sealed class FeedbackWebhook(in WebhookService webhookService)
{
    private readonly WebhookService _webhookService = webhookService;

    /// <summary>
    ///     Sends a feedback submission with a zipped log and optional screenshot.
    /// </summary>
    public async Task<Result<Guid>> SendAsync(
        string? description,
        string? contact,
        NamedStream log,
        NamedStream? screenshot = null,
        CancellationToken cancellationToken = default
    )
    {
        Result<byte[]> compressResult = Zip.Compress(log);
        if (!compressResult.Success)
        {
            return new Result<Guid>(success: false, value: Guid.Empty, $"Failed to compress feedback attachments. {compressResult.Message}", compressResult.Exception);
        }

        string title = !string.IsNullOrWhiteSpace(description) ? description.Truncate(count: 20) : "Quick Submission";
        return await PostArchiveAsync(
            archiveBytes: compressResult.Value,
            archiveNamePrefix: log.Name,
            description: description,
            contact: contact,
            threadTitle: title,
            screenshot: screenshot,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }

    /// <summary>
    ///     Packages a crash report, session log, and optional dump (within <paramref name="maxUploadBytes"/>) and posts it to the feedback webhook.
    /// </summary>
    public async Task<Result<Guid>> SendCrashAsync(
        string? description,
        string? contact,
        string faultSummary,
        string reportPath,
        string? sessionLogPath,
        string? dumpPath,
        string? crashReportJsonPath,
        int maxUploadBytes,
        CancellationToken cancellationToken = default
    )
    {
        Result<(byte[] ArchiveBytes, bool IncludedDump)> archiveResult = BuildCrashArchive(
            reportPath,
            sessionLogPath,
            dumpPath,
            crashReportJsonPath,
            maxUploadBytes
        );

        if (!archiveResult.Success)
        {
            return new Result<Guid>(success: false, value: Guid.Empty, archiveResult.Message, archiveResult.Exception);
        }

        var body = new StringBuilder();
        body.AppendLine($"[Crash] {faultSummary}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            body.AppendLine();
            body.AppendLine(description.Trim());
        }

        if (!string.IsNullOrWhiteSpace(dumpPath) && !archiveResult.Value.IncludedDump)
        {
            body.AppendLine();
            body.AppendLine($"Note: Crash dump exceeded upload limit and was saved locally at: {dumpPath}");
        }

        string rawTitle = !string.IsNullOrWhiteSpace(description) ? description.Trim() : faultSummary;
        string titleSource = rawTitle.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        string threadTitle = $"[Crash] {titleSource.Truncate(count: 60)}";

        return await PostArchiveAsync(
            archiveBytes: archiveResult.Value.ArchiveBytes,
            archiveNamePrefix: Path.GetFileNameWithoutExtension(reportPath),
            description: body.ToString().TrimEnd(),
            contact: contact,
            threadTitle: threadTitle,
            screenshot: null,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }

    /// <summary>
    ///     Builds a ZIP archive containing the crash report, session log, and optional crash dump if within <paramref name="maxUploadBytes"/>.
    /// </summary>
    public static Result<(byte[] ArchiveBytes, bool IncludedDump)> BuildCrashArchive(
        string reportPath,
        string? sessionLogPath,
        string? dumpPath,
        string? crashReportJsonPath,
        int maxUploadBytes
    )
    {
        List<string> baseFiles = [];
        if (File.Exists(reportPath))
        {
            baseFiles.Add(reportPath);
        }

        if (!string.IsNullOrWhiteSpace(sessionLogPath) &&
            File.Exists(sessionLogPath) &&
            !Path.GetFullPath(sessionLogPath).Equals(Path.GetFullPath(reportPath), StringComparison.OrdinalIgnoreCase))
        {
            baseFiles.Add(sessionLogPath);
        }

        if (!string.IsNullOrWhiteSpace(crashReportJsonPath) && File.Exists(crashReportJsonPath))
        {
            baseFiles.Add(crashReportJsonPath);
        }

        if (baseFiles.Count == 0)
        {
            return Result<(byte[], bool)>.FromFailure("No crash report or log files were found to attach.");
        }

        if (!string.IsNullOrWhiteSpace(dumpPath) && File.Exists(dumpPath))
        {
            long dumpLength = new FileInfo(dumpPath).Length;
            bool plausibleUnderCap = maxUploadBytes <= 0 || dumpLength <= (long)maxUploadBytes * 8;
            if (plausibleUnderCap)
            {
                List<string> withDump = [.. baseFiles, dumpPath];
                Result<byte[]> withDumpZip = CompressFiles(withDump);
                if (withDumpZip.Success && (maxUploadBytes <= 0 || withDumpZip.Value.Length <= maxUploadBytes))
                {
                    return Result<(byte[], bool)>.FromSuccess((withDumpZip.Value, true));
                }
            }
        }

        Result<byte[]> baseZip = CompressFiles(baseFiles);
        if (!baseZip.Success)
        {
            return new Result<(byte[], bool)>(success: false, default, baseZip.Message, baseZip.Exception);
        }

        return Result<(byte[], bool)>.FromSuccess((baseZip.Value, false));
    }

    private async Task<Result<Guid>> PostArchiveAsync(
        byte[] archiveBytes,
        string archiveNamePrefix,
        string? description,
        string? contact,
        string threadTitle,
        NamedStream? screenshot,
        CancellationToken cancellationToken
    )
    {
        var guid = Guid.NewGuid();
        using var form = new MultipartFormDataContent();

        var logAttachment = new ByteArrayContent(archiveBytes);
        logAttachment.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(logAttachment, name: "log", fileName: $"{archiveNamePrefix}_{guid}.zip");

        if (screenshot.HasValue)
        {
            var screenshotAttachment = new StreamContent(screenshot.Value.Value);
            screenshotAttachment.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(screenshotAttachment, name: "screenshot", fileName: $"{screenshot.Value.Name}_{guid}.png");
        }

        if (!string.IsNullOrWhiteSpace(contact))
        {
            form.Add(new StringContent(contact), name: "username");
        }

        string contentText = $"{guid}\n\n{description}";
        if (contentText.Length > 1950)
        {
            contentText = contentText[..1950];
        }

        form.Add(new StringContent(contentText), name: "content");
        form.Add(new StringContent(threadTitle), name: "thread_name");

        Result<Uri> feedbackUriResult = await _webhookService.ResolveFeedbackUriAsync(cancellationToken).ConfigureAwait(false);
        if (!feedbackUriResult.Success)
        {
            return new Result<Guid>(success: false, value: guid, $"Failed to resolve feedback URI. {feedbackUriResult.Message}", feedbackUriResult.Exception);
        }

        try
        {
            using var httpClient = new HttpClient();
            using HttpResponseMessage response = await httpClient.PostAsync(feedbackUriResult.Value, form, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<Guid>.FromFailure($"Failed to send report. Received status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            return new Result<Guid>(success: false, value: guid, "Failed to send report.", ex);
        }

        return Result<Guid>.FromSuccess(guid);
    }

    private static Result<byte[]> CompressFiles(IReadOnlyList<string> filePaths)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < filePaths.Count; i++)
                {
                    string filePath = filePaths[i];
                    string entryName = Path.GetFileName(filePath);
                    if (!usedNames.Add(entryName))
                    {
                        entryName = $"{i}_{entryName}";
                    }

                    ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using Stream entryStream = entry.Open();
                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    fileStream.CopyTo(entryStream);
                }
            }

            return Result<byte[]>.FromSuccess(memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            return new Result<byte[]>(success: false, null!, "Caught an exception during compression", ex);
        }
    }
}
