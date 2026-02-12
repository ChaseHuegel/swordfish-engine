using System;
using System.Net.Http;
using System.Threading.Tasks;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.IO;

namespace WaywardBeyond.Client.Core.Services;

internal class FeedbackWebhook(in WebhookService webhookService)
{
    private readonly WebhookService _webhookService = webhookService;
    
    public async Task<Result> SendAsync(string? message, string? email, NamedStream log, NamedStream screenshot)
    {
        Result<byte[]> compressResult = Zip.Compress(log, screenshot);
        if (!compressResult.Success)
        {
            return new Result(success: false, $"Failed to compress feedback attachments. {compressResult.Message}", compressResult.Exception);
        }
        
        using var form = new MultipartFormDataContent();
        
        var content = new ByteArrayContent(compressResult.Value);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        form.Add(content, name: "feedback-content", fileName: "feedback-content.zip");
        
        if (email != null)
        {
            form.Add(new StringContent(email), name: "email");
        }

        if (message != null)
        {
            form.Add(new StringContent(message), name: "message");
        }

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