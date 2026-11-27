using WaywardBeyond.Bricks;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Maps a pressed <see cref="InteractionInput"/> channel and the held item to the wire interaction edge it
/// produces. Primary drives every item that has a primary action: a modifier tool applies its paint, a
/// placeable item places its brick, and a removal tool breaks the targeted brick. Secondary is bound only by
/// modifier tools to clear their paint. An item without an action for an input produces no edge.
/// </summary>
internal static class InteractionIntent
{
    /// <summary>Returns the input edge the pressed <paramref name="input"/> produces with the held item.</summary>
    public static InteractionKind For(InteractionInput input, Item? heldItem)
    {
        bool isModifierTool = PaintMode(heldItem) != null;

        if (input == InteractionInput.Primary && (isModifierTool || HasPrimaryBrickAction(heldItem)))
        {
            return InteractionKind.PrimaryPressed;
        }

        if (input == InteractionInput.Secondary && isModifierTool)
        {
            return InteractionKind.SecondaryPressed;
        }

        return InteractionKind.None;
    }

    /// <summary>
    /// Returns the face-paint mode the held item's tool applies, or null when the item is not a modifier
    /// tool.
    /// </summary>
    public static PaintKind? PaintMode(Item? heldItem)
    {
        return heldItem?.Tool?.Type switch
        {
            ToolType.Sprayer => PaintKind.Color,
            ToolType.Drill => PaintKind.Texture,
            _ => null,
        };
    }

    /// <summary>
    /// Whether the item places or breaks a brick. The shared resolver derives which one from the held
    /// placeable, so this only decides whether the primary edge does anything at all.
    /// </summary>
    private static bool HasPrimaryBrickAction(Item? heldItem)
    {
        return heldItem?.Tool?.Type == ToolType.Mining
            || heldItem?.Placeable?.Type == PlaceableType.Brick;
    }
}
