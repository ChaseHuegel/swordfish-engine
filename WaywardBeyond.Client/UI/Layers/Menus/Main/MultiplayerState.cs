using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Dynamic state for the multiplayer page.</summary>
internal readonly record struct MultiplayerState(
    bool Remote,
    string BannerServer,
    bool Favorites,
    bool Scanning,
    string EmptyMessage,
    bool EmptyFavorites,
    IReadOnlyList<ServerRowState> Rows,
    string? Error,
    bool JoinEnabled
);

/// <summary>One server row.</summary>
internal readonly record struct ServerRowState(
    string Name,
    string Address,
    string Players,
    bool Selected,
    bool Favorite
);
