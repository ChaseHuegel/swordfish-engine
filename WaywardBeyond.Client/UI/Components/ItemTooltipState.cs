using System.Collections.Generic;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>One horizontal row of trait badges inside an item tooltip.</summary>
internal readonly record struct ItemTooltipTraitRowState(IReadOnlyList<ItemTooltipTrait> Traits);

/// <summary>Display data and localization keys for a floating item hover tooltip.</summary>
internal readonly record struct ItemTooltipState(
    bool Visible,
    string AnchorID,
    string NameKey,
    ItemTooltipKind Kind,
    string? DescriptionKey,
    IReadOnlyList<ItemTooltipTraitRowState> TraitRows,
    ILocalization Localization,
    string DebugID = ""
)
{
    /// <summary>Creates a hidden tooltip state.</summary>
    public static ItemTooltipState Hidden(ILocalization localization)
    {
        return new ItemTooltipState(
            Visible: false,
            AnchorID: string.Empty,
            NameKey: string.Empty,
            Kind: ItemTooltipKind.Item,
            DescriptionKey: null,
            TraitRows: [],
            Localization: localization
        );
    }

    /// <summary>Gets whether the hovered item has no known definition.</summary>
    public bool IsUnknown => Kind == ItemTooltipKind.Unknown;

    /// <summary>Gets whether the hovered item places a brick.</summary>
    public bool IsBrick => Kind == ItemTooltipKind.Brick;

    /// <summary>Gets whether the hovered item is a mining tool.</summary>
    public bool IsMiningTool => Kind == ItemTooltipKind.MiningTool;

    /// <summary>Gets whether the hovered item is a conversion tool.</summary>
    public bool IsConversionTool => Kind == ItemTooltipKind.ConversionTool;

    /// <summary>Gets whether the tooltip displays a debug item id subtitle row.</summary>
    public bool HasDebugID => !string.IsNullOrEmpty(DebugID);

    /// <summary>Gets whether the tooltip has a resolved non-empty description.</summary>
    public bool HasDescription => !string.IsNullOrEmpty(ResolvedDescription);

    /// <summary>Gets whether the tooltip has any trait badge rows.</summary>
    public bool HasTraits => TraitRows.Count > 0;

    /// <summary>Gets the resolved item display name, falling back to <see cref="NameKey"/>.</summary>
    public string ResolvedName => L(NameKey);

    /// <summary>Gets the resolved item description, or empty when absent or missing from localization.</summary>
    public string ResolvedDescription => string.IsNullOrEmpty(DescriptionKey)
        ? string.Empty
        : Localization.GetString(DescriptionKey) ?? string.Empty;

    /// <summary>Resolves a localization key, falling back to the key itself.</summary>
    public string L(string key) => Localization.GetString(key) ?? key;

    /// <summary>Returns whether a trait is the rotatable trait.</summary>
    public bool IsOrientable(ItemTooltipTrait trait) => trait == ItemTooltipTrait.Orientable;

    /// <summary>Returns whether a trait is the shapeable trait.</summary>
    public bool IsShapeable(ItemTooltipTrait trait) => trait == ItemTooltipTrait.Shapeable;

    /// <summary>Returns whether a trait is the light-source trait.</summary>
    public bool IsLightSource(ItemTooltipTrait trait) => trait == ItemTooltipTrait.LightSource;

    /// <summary>Returns whether a trait is the propulsion trait.</summary>
    public bool IsPropulsion(ItemTooltipTrait trait) => trait == ItemTooltipTrait.Propulsion;
}
