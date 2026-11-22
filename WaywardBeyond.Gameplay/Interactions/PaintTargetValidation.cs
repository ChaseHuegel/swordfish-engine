namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Result of validating a paint-apply target against brick paintability and alpha-bucket rules.</summary>
public enum PaintTargetValidation : byte
{
    Valid = 0,
    EmptyCell = 1,
    UnknownBrick = 2,
    NotPaintable = 3,
    MissingPaint = 4,
    AlphaMismatch = 5,
}