using System;

namespace WaywardBeyond.Client.Services;

internal record struct Webhooks(Uri FeedbackSourceUri, Uri DiscordUri, Uri SteamUri);