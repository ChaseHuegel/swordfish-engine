using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Networking;

/// <summary>The server's level listing plus the menu capabilities that arrive with it.</summary>
internal readonly record struct LevelListing(Level[] Levels, bool CanCreateSave);
