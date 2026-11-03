using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Dynamic state for the multiplayer page.</summary>
internal readonly record struct MultiplayerState(
    bool Remote,
    string BannerName,
    string BannerHost,
    int BannerPort,
    bool Favorites,
    bool Scanning,
    string DiscoverKey,
    IReadOnlyList<ServerRowState> Rows,
    string ErrorKey,
    bool JoinEnabled
);

/// <summary>One server row.</summary>
internal readonly record struct ServerRowState(
    string Name,
    string Host,
    int Port,
    string Players,
    bool Selected,
    bool Favorite
);
