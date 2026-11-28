using System;

namespace WaywardBeyond.Items;

/// <summary>Canonical invariant lang tag keys for item groups and slot filters.</summary>
public static class ItemTags
{
    /// <summary>Tag for all tool items.</summary>
    public const string TOOL = "item.tool";

    /// <summary>Tag for all placeable brick and blueprint items.</summary>
    public const string PLACEABLE = "item.placeable";

    /// <summary>Tag for mining tools.</summary>
    public const string TOOL_MINING = "item.tool.mining";

    /// <summary>Tag for paint sprayer tools.</summary>
    public const string TOOL_SPRAYER = "item.tool.sprayer";

    /// <summary>Tag for texture drill tools.</summary>
    public const string TOOL_DRILL = "item.tool.drill";

    /// <summary>Tag for all wearable equipment items.</summary>
    public const string WEARABLE = "item.wearable";

    /// <summary>Tag for helmet wearable items.</summary>
    public const string WEARABLE_HELMET = "item.wearable.helmet";

    /// <summary>Tag for torso wearable items.</summary>
    public const string WEARABLE_TORSO = "item.wearable.torso";

    /// <summary>Tag for leg wearable items.</summary>
    public const string WEARABLE_LEGS = "item.wearable.legs";

    /// <summary>Tag for head accessory wearable items.</summary>
    public const string WEARABLE_HEAD = "item.wearable.head";

    /// <summary>Tag for suit wearable items.</summary>
    public const string WEARABLE_SUIT = "item.wearable.suit";

    /// <summary>Tag for oxygen tank wearable items.</summary>
    public const string WEARABLE_O2 = "item.wearable.o2";

    /// <summary>Tag for cybernetic augment items.</summary>
    public const string AUGMENT = "item.augment";

    /// <summary>Tag for fuel items accepted by fuel slots.</summary>
    public const string FUEL = "item.fuel";

    /// <summary>
    /// Resolves a short equip slot tag (such as <c>helmet</c>, <c>suit</c>, <c>o2</c>, or <c>augment</c>)
    /// or full invariant tag key into its canonical slot filter tag.
    /// </summary>
    public static bool TryResolveEquipTag(string? slotTag, out string resolvedTag)
    {
        if (string.IsNullOrWhiteSpace(slotTag))
        {
            resolvedTag = string.Empty;
            return false;
        }

        string trimmed = slotTag.Trim();
        if (string.Equals(trimmed, "helmet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, WEARABLE_HELMET, StringComparison.OrdinalIgnoreCase))
        {
            resolvedTag = WEARABLE_HELMET;
            return true;
        }

        if (string.Equals(trimmed, "torso", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, WEARABLE_TORSO, StringComparison.OrdinalIgnoreCase))
        {
            resolvedTag = WEARABLE_TORSO;
            return true;
        }

        if (string.Equals(trimmed, "legs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, WEARABLE_LEGS, StringComparison.OrdinalIgnoreCase))
        {
            resolvedTag = WEARABLE_LEGS;
            return true;
        }

        if (string.Equals(trimmed, "head", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, WEARABLE_HEAD, StringComparison.OrdinalIgnoreCase))
        {
            resolvedTag = WEARABLE_HEAD;
            return true;
        }

        if (string.Equals(trimmed, "suit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, WEARABLE_SUIT, StringComparison.OrdinalIgnoreCase))
        {
            resolvedTag = WEARABLE_SUIT;
            return true;
        }

        if (string.Equals(trimmed, "o2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, WEARABLE_O2, StringComparison.OrdinalIgnoreCase))
        {
            resolvedTag = WEARABLE_O2;
            return true;
        }

        if (string.Equals(trimmed, "augment", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, AUGMENT, StringComparison.OrdinalIgnoreCase))
        {
            resolvedTag = AUGMENT;
            return true;
        }

        if (string.Equals(trimmed, WEARABLE, StringComparison.OrdinalIgnoreCase))
        {
            resolvedTag = WEARABLE;
            return true;
        }

        //  Allow custom namespaced or dotted mod slot tags without whitespace.
        if ((trimmed.Contains(':') || trimmed.Contains('.')) && !HasWhitespace(trimmed))
        {
            resolvedTag = trimmed;
            return true;
        }

        resolvedTag = string.Empty;
        return false;
    }

    /// <summary>Returns whether a resolved equip tag satisfies <paramref name="requiredTag"/>.</summary>
    public static bool EquipMatchesTag(string equipTag, string requiredTag)
    {
        if (string.IsNullOrEmpty(equipTag) || string.IsNullOrEmpty(requiredTag))
        {
            return false;
        }

        if (string.Equals(equipTag, requiredTag, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(requiredTag, WEARABLE, StringComparison.Ordinal)
            && equipTag.StartsWith("item.wearable.", StringComparison.Ordinal);
    }

    private static bool HasWhitespace(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                return true;
            }
        }

        return false;
    }
}
