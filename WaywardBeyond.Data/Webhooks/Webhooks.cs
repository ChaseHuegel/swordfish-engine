using System;

namespace WaywardBeyond.Data.Webhooks;

/// <summary>
///     Remote endpoint URIs used for feedback, crash reporting, Discord, and Steam links.
/// </summary>
public readonly record struct Webhooks(Uri FeedbackSourceUri, Uri DiscordUri, Uri SteamUri)
{
    /// <summary>
    ///     Default Wayward Beyond webhook endpoints.
    /// </summary>
    public static Webhooks Default { get; } = new(
        FeedbackSourceUri: new Uri("https://gist.githubusercontent.com/ChaseHuegel/ed8b4d594789c4567e352148e006dbc1/raw/wb-feedback-webhook.txt"),
        DiscordUri: new Uri("https://gist.githubusercontent.com/ChaseHuegel/fce325f4a4d41684277994eaa730dc5b/raw/wb-discord"),
        SteamUri: new Uri("https://gist.githubusercontent.com/ChaseHuegel/c3b57b884e2c675fcbffdba869d2f05d/raw/wb-steam")
    );
}
