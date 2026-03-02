using System;
using System.Net.Http;
using System.Threading.Tasks;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Services;

internal class WebhookService(in Webhooks webhooks)
{
    private readonly Webhooks _webhooks = webhooks;

    public async Task<Result<Uri>> ResolveFeedbackUriAsync()
    {
        using var client = new HttpClient();
        try 
        {
            string url = await client.GetStringAsync(_webhooks.FeedbackSourceUri);
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                return Result<Uri>.FromFailure($"Invalid URI \"{url}\"");
            }
            
            return Result<Uri>.FromSuccess(uri);
        }
        catch (Exception ex)
        {
            return Result<Uri>.FromFailure(ex);
        }
    }
}