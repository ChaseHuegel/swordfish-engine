using Swordfish.Library.IO;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Maps a pressed mouse button and the held item to the interaction edge the button produces. The left
/// button is the primary edge for every item that has a primary action: a modifier tool applies its paint,
/// a placeable item places its brick, and a removal tool breaks the targeted brick. The right button is the
/// secondary edge and only a modifier tool binds it, to clear its paint. An item without an action for a
/// button produces no edge.
/// </summary>
internal static class InteractionIntent
{
    /// <summary>Returns the input edge the pressed button produces with the held item.</summary>
    public static InteractionKind For(MouseButton button, Item? heldItem)
    {
        bool isModifierTool = PaintMode(heldItem) != null;

        if (button == MouseButton.Left && (isModifierTool || HasPrimaryBrickAction(heldItem)))
        {
            return InteractionKind.PrimaryPressed;
        }

        if (button == MouseButton.Right && isModifierTool)
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
