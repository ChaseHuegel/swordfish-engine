namespace WaywardBeyond.Client.UI.Layers.Blueprints;

/// <summary>Display row state for one saved blueprint entry in the Blueprint Library.</summary>
internal readonly record struct BlueprintListItemState(
    string Id,
    string Name,
    int SizeX,
    int SizeY,
    int SizeZ,
    int BrickCount,
    bool IsRenaming,
    bool ConfirmingDelete
);
