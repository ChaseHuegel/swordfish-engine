using Swordfish.Library.Collections;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Data;

/// <summary>Provides access to registered <see cref="SlotGroupSchema"/> definitions.</summary>
public interface ISlotGroupSchemaDatabase : IAssetDatabase<SlotGroupSchema>
{
    /// <summary>Returns true when <paramref name="itemId"/> is allowed in <paramref name="slot"/>.</summary>
    bool AllowsItem(in SlotId slot, string? itemId, LocalizedTags? tags);

    /// <summary>Gets the configured capacity for <paramref name="groupId"/>, or <paramref name="fallback"/> when unregistered.</summary>
    int GetCapacity(string groupId, int fallback = 0);
}
