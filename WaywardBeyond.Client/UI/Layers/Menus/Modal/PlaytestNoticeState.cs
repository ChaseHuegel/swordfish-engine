namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Display data for the playtest notice modal.</summary>
internal readonly record struct PlaytestNoticeState(
    string Title,
    string Body,
    string Ok,
    string DiscordGlyph,
    string SteamGlyph
);
