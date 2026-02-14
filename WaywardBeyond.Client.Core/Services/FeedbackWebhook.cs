using System;
using System.Net.Http;
using System.Threading.Tasks;
using Swordfish.Library.Extensions;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.IO;

namespace WaywardBeyond.Client.Core.Services;

internal class FeedbackWebhook(in WebhookService webhookService)
{
    private readonly WebhookService _webhookService = webhookService;
    
    public async Task<Result> SendAsync(string? description, string? contact, NamedStream log, NamedStream screenshot)
    {
        Result<byte[]> compressResult = Zip.Compress(log);
        if (!compressResult.Success)
        {
            return new Result(success: false, $"Failed to compress feedback attachments. {compressResult.Message}", compressResult.Exception);
        }

        var guid = Guid.NewGuid();
        
        using var form = new MultipartFormDataContent();
        
        var logAttachment = new ByteArrayContent(compressResult.Value);
        logAttachment.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        form.Add(logAttachment, name: "log", fileName: $"{log.Name}_{guid}.zip");

        var screenshotAttachment = new StreamContent(screenshot.Value);
        screenshotAttachment.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(screenshotAttachment, name: "screenshot", fileName: $"{screenshot.Name}_{guid}.png");
        
        if (!string.IsNullOrWhiteSpace(contact))
        {
            form.Add(new StringContent(contact), name: "username");
        }

        form.Add(new StringContent($"{guid}\n\n{description}"), name: "content");

        string title = !string.IsNullOrWhiteSpace(description) ? description.Truncate(count: 20) : "Quick Submission";
        form.Add(new StringContent(title), name: "thread_name");
        
        Result<Uri> feedbackUriResult = await _webhookService.ResolveFeedbackUriAsync();
        if (!feedbackUriResult.Success)
        {
            return new Result(success: false, $"Failed to resolve feedback URI. {feedbackUriResult.Message}", feedbackUriResult.Exception);
        }
        
        try
        {
            using var httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.PostAsync(feedbackUriResult.Value, form);
            if (!response.IsSuccessStatusCode)
            {
                return Result.FromFailure($"Failed to send feedback. Received status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        { 
            return new Result(success: false, "Failed to send feedback.", ex);
        }
        
        return Result.FromSuccess();
    }
}