using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Webhooks;

/// <summary>
///     Resolves remote webhook and community link URIs.
/// </summary>
public sealed class WebhookService(in Webhooks webhooks)
{
    private readonly Webhooks _webhooks = webhooks;

    /// <summary>
    ///     Resolves the feedback and crash report webhook URI.
    /// </summary>
    public Task<Result<Uri>> ResolveFeedbackUriAsync(CancellationToken cancellationToken = default)
    {
        return ResolveUriAsync(_webhooks.FeedbackSourceUri, cancellationToken);
    }

    /// <summary>
    ///     Resolves the Discord invite URI.
    /// </summary>
    public Task<Result<Uri>> ResolveDiscordUriAsync(CancellationToken cancellationToken = default)
    {
        return ResolveUriAsync(_webhooks.DiscordUri, cancellationToken);
    }

    /// <summary>
    ///     Resolves the Steam store page URI.
    /// </summary>
    public Task<Result<Uri>> ResolveSteamUriAsync(CancellationToken cancellationToken = default)
    {
        return ResolveUriAsync(_webhooks.SteamUri, cancellationToken);
    }

    private static async Task<Result<Uri>> ResolveUriAsync(Uri sourceUri, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        try
        {
            string url = (await client.GetStringAsync(sourceUri, cancellationToken).ConfigureAwait(false)).Trim();
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
