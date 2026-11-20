namespace Swordfish.Graphics;

/// <summary>Identifies one transparent draw item for cross-stage back-to-front sorting.</summary>
internal readonly struct TransparentDrawItem(int stageIndex, int itemIndex, float distanceSq)
{
    public readonly int StageIndex = stageIndex;
    public readonly int ItemIndex = itemIndex;
    public readonly float DistanceSq = distanceSq;
}
