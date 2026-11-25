namespace WaywardBeyond.Data.Blueprints;

/// <summary>Metadata summary for one stored player blueprint.</summary>
public readonly record struct BlueprintSummary(
    string Id,
    string Name,
    int SizeX,
    int SizeY,
    int SizeZ,
    int BrickCount,
    long CreatedAtMs,
    long ModifiedAtMs
);
