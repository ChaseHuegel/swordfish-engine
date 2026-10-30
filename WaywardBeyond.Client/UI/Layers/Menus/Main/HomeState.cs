using Swordfish.Graphics;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Dynamic state for the home menu and its Continue card.</summary>
internal readonly record struct HomeState(bool HasLastSession, HomeCardState Card);

/// <summary>Dynamic facts shown by the Continue card.</summary>
internal readonly record struct HomeCardState(
    bool Remote,
    string Destination,
    string SaveName,
    GameMode Mode,
    string SeedPhrase,
    long LastPlayedMs,
    long AgeMs,
    string CharacterName,
    string Species,
    int Level,
    Material Avatar
);
