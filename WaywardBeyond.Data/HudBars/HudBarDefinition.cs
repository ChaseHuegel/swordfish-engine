using System.Numerics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.HudBars;

/// <summary>Defines a data-driven HUD icon bar bound to replicated entity statistics.</summary>
public readonly record struct HudBarDefinition
{
    /// <summary>Default icon count when <c>icon_count</c> is omitted.</summary>
    public const int DEFAULT_ICON_COUNT = 10;

    /// <summary>Default warning threshold ratio when <c>warn_threshold</c> is omitted.</summary>
    public const float DEFAULT_WARN_THRESHOLD = 0.3f;

    /// <summary>Default critical threshold ratio when <c>critical_threshold</c> is omitted.</summary>
    public const float DEFAULT_CRITICAL_THRESHOLD = 0.15f;

    /// <summary>Unique namespaced HUD bar ID (for example <c>wb:health</c>).</summary>
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

    /// <summary>Validates all fields and returns a normalized <see cref="HudBarDefinition"/>.</summary>
    public Result<HudBarDefinition> Validate()
    {
        if (string.IsNullOrWhiteSpace(ID))
        {
            return Result<HudBarDefinition>.FromFailure("HudBar definition is missing its ID.");
        }

        string id = ID.Trim();
        if (string.IsNullOrWhiteSpace(StatId))
        {
            return Result<HudBarDefinition>.FromFailure($"HudBar \"{id}\" is missing stat_id.");
        }

        string? maxStatId = string.IsNullOrWhiteSpace(MaxStatId) ? null : MaxStatId.Trim();
        if (DefaultMax.HasValue && (!float.IsFinite(DefaultMax.Value) || DefaultMax.Value <= 0f))
        {
            return Result<HudBarDefinition>.FromFailure($"HudBar \"{id}\" default_max must be finite and > 0.");
        }

        if (maxStatId == null && !DefaultMax.HasValue)
        {
            return Result<HudBarDefinition>.FromFailure($"HudBar \"{id}\" must specify max_stat_id or a positive default_max.");
        }

        if (IconCount < 1)
        {
            return Result<HudBarDefinition>.FromFailure($"HudBar \"{id}\" icon_count must be at least 1.");
        }

        if (string.IsNullOrWhiteSpace(IconFull)
            || string.IsNullOrWhiteSpace(IconHalf)
            || string.IsNullOrWhiteSpace(IconEmpty))
        {
            return Result<HudBarDefinition>.FromFailure($"HudBar \"{id}\" must define icon_full, icon_half, and icon_empty.");
        }

        if (!float.IsFinite(WarnThreshold) || WarnThreshold < 0f || WarnThreshold > 1f)
        {
            return Result<HudBarDefinition>.FromFailure($"HudBar \"{id}\" warn_threshold must be within [0, 1].");
        }

        if (!float.IsFinite(CriticalThreshold) || CriticalThreshold < 0f || CriticalThreshold > 1f)
        {
            return Result<HudBarDefinition>.FromFailure($"HudBar \"{id}\" critical_threshold must be within [0, 1].");
        }

        if (CriticalThreshold > WarnThreshold)
        {
            return Result<HudBarDefinition>.FromFailure($"HudBar \"{id}\" critical_threshold ({CriticalThreshold}) cannot exceed warn_threshold ({WarnThreshold}).");
        }

        string? colorTint = null;
        Vector4? colorTintRgba = null;
        if (!string.IsNullOrWhiteSpace(ColorTint))
        {
            if (!HexColor.TryParseRgba(ColorTint, out Vector4 parsed))
            {
                return Result<HudBarDefinition>.FromFailure($"HudBar \"{id}\" has invalid color_tint \"{ColorTint}\".");
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
        return Result<HudBarDefinition>.FromSuccess(normalized);
    }
}
