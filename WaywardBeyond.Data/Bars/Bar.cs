using System.Numerics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Bars;

/// <summary>Validated data-driven icon bar bound to replicated entity statistics.</summary>
public readonly record struct Bar
{
    /// <summary>Default icon count when <c>IconCount</c> is omitted.</summary>
    public const int DEFAULT_ICON_COUNT = 10;

    /// <summary>Default warning threshold ratio when <c>WarnThreshold</c> is omitted.</summary>
    public const float DEFAULT_WARN_THRESHOLD = 0.3f;

    /// <summary>Default critical threshold ratio when <c>CriticalThreshold</c> is omitted.</summary>
    public const float DEFAULT_CRITICAL_THRESHOLD = 0.15f;

    /// <summary>Unique namespaced bar ID (for example <c>wb:health</c>).</summary>
    public string ID { get; init; }

    /// <summary>Stat ID providing the bar's current value.</summary>
    public string StatId { get; init; }

    /// <summary>Optional stat ID providing the bar's maximum value.</summary>
    public string? MaxStatId { get; init; }

    /// <summary>Optional fallback maximum used when <see cref="MaxStatId"/> is omitted or absent.</summary>
    public float? DefaultMax { get; init; }

    /// <summary>Number of discrete icons rendered across the bar.</summary>
    public int IconCount { get; init; }

    /// <summary>Texture asset key for a full icon step.</summary>
    public string IconFull { get; init; }

    /// <summary>Texture asset key for a half icon step.</summary>
    public string IconHalf { get; init; }

    /// <summary>Texture asset key for an empty icon step.</summary>
    public string IconEmpty { get; init; }

    /// <summary>Normalized ratio threshold at or below which warning state activates.</summary>
    public float WarnThreshold { get; init; }

    /// <summary>Normalized ratio threshold at or below which critical state activates.</summary>
    public float CriticalThreshold { get; init; }

    /// <summary>Vertical stacking order (lower values render first).</summary>
    public int Order { get; init; }

    /// <summary>Whether the bar hides while the player is in Creative mode.</summary>
    public bool HideInCreative { get; init; }

    /// <summary>Optional hex color tint string.</summary>
    public string? ColorTint { get; init; }

    /// <summary>Parsed normalized sRGBA tint color when <see cref="ColorTint"/> is set.</summary>
    public Vector4? ColorTintRgba { get; init; }

    /// <summary>Validates all fields and returns a normalized <see cref="Bar"/>.</summary>
    public Result<Bar> Validate()
    {
        if (string.IsNullOrWhiteSpace(ID))
        {
            return Result<Bar>.FromFailure("Bar is missing its ID.");
        }

        string id = ID.Trim();
        if (string.IsNullOrWhiteSpace(StatId))
        {
            return Result<Bar>.FromFailure($"Bar \"{id}\" is missing StatId.");
        }

        string? maxStatId = string.IsNullOrWhiteSpace(MaxStatId) ? null : MaxStatId.Trim();
        if (DefaultMax.HasValue && (!float.IsFinite(DefaultMax.Value) || DefaultMax.Value <= 0f))
        {
            return Result<Bar>.FromFailure($"Bar \"{id}\" DefaultMax must be finite and > 0.");
        }

        if (maxStatId == null && !DefaultMax.HasValue)
        {
            return Result<Bar>.FromFailure($"Bar \"{id}\" must specify MaxStatId or a positive DefaultMax.");
        }

        if (IconCount < 1)
        {
            return Result<Bar>.FromFailure($"Bar \"{id}\" IconCount must be at least 1.");
        }

        if (string.IsNullOrWhiteSpace(IconFull)
            || string.IsNullOrWhiteSpace(IconHalf)
            || string.IsNullOrWhiteSpace(IconEmpty))
        {
            return Result<Bar>.FromFailure($"Bar \"{id}\" must define IconFull, IconHalf, and IconEmpty.");
        }

        if (!float.IsFinite(WarnThreshold) || WarnThreshold < 0f || WarnThreshold > 1f)
        {
            return Result<Bar>.FromFailure($"Bar \"{id}\" WarnThreshold must be within [0, 1].");
        }

        if (!float.IsFinite(CriticalThreshold) || CriticalThreshold < 0f || CriticalThreshold > 1f)
        {
            return Result<Bar>.FromFailure($"Bar \"{id}\" CriticalThreshold must be within [0, 1].");
        }

        if (CriticalThreshold > WarnThreshold)
        {
            return Result<Bar>.FromFailure($"Bar \"{id}\" CriticalThreshold ({CriticalThreshold}) cannot exceed WarnThreshold ({WarnThreshold}).");
        }

        string? colorTint = null;
        Vector4? colorTintRgba = null;
        if (ColorTint != null)
        {
            if (string.IsNullOrWhiteSpace(ColorTint) || !HexColor.TryParseRgba(ColorTint, out Vector4 parsed))
            {
                return Result<Bar>.FromFailure($"Bar \"{id}\" has invalid ColorTint \"{ColorTint}\".");
            }

            colorTint = ColorTint.Trim();
            colorTintRgba = parsed;
        }

        var normalized = this with
        {
            ID = id,
            StatId = StatId.Trim(),
            MaxStatId = maxStatId,
            IconFull = IconFull.Trim(),
            IconHalf = IconHalf.Trim(),
            IconEmpty = IconEmpty.Trim(),
            ColorTint = colorTint,
            ColorTintRgba = colorTintRgba,
        };
        return Result<Bar>.FromSuccess(normalized);
    }
}
