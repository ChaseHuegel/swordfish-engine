using Swordfish.Library.Collections;

namespace WaywardBeyond.Data.Localization;

/// <summary>Provides access to loaded localized tag lists.</summary>
public interface ILocalizedTagsDatabase : IAssetDatabase<LocalizedTags>
{
    /// <summary>The invariant (empty-language) tag lists, or null when none are loaded.</summary>
    LocalizedTags? Invariant { get; }
}