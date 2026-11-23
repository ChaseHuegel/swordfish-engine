#nullable enable
using Swordfish.Library.IO;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Items;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>
/// Pins the interaction control scheme: the left button is the primary edge for every item that has a
/// primary action, only a modifier tool binds the right button to the secondary edge, and an item with no
/// action for a button produces no edge.
/// </summary>
public class InteractionIntentTests
{
    [Fact]
    public void APlaceableItemPressesThePrimaryEdgeOnTheLeftButton()
    {
        Item panel = Placeable("wb:panel");

        Assert.Equal(InteractionKind.PrimaryPressed, InteractionIntent.For(MouseButton.Left, panel));
        Assert.Equal(InteractionKind.None, InteractionIntent.For(MouseButton.Right, panel));
        Assert.Null(InteractionIntent.PaintMode(panel));
    }

    [Fact]
    public void ARemovalToolPressesThePrimaryEdgeOnTheLeftButton()
    {
        Item laser = Tool("laser", ToolType.Mining);

        Assert.Equal(InteractionKind.PrimaryPressed, InteractionIntent.For(MouseButton.Left, laser));
        Assert.Equal(InteractionKind.None, InteractionIntent.For(MouseButton.Right, laser));
    }

    [Theory]
    [InlineData(ToolType.Sprayer, PaintKind.Color)]
    [InlineData(ToolType.Drill, PaintKind.Texture)]
    public void AModifierToolPressesThePrimaryEdgeOnTheLeftButtonAndTheSecondaryEdgeOnTheRight(ToolType toolType, PaintKind paintMode)
    {
        Item tool = Tool("wb:modifier", toolType);

        Assert.Equal(InteractionKind.PrimaryPressed, InteractionIntent.For(MouseButton.Left, tool));
        Assert.Equal(InteractionKind.SecondaryPressed, InteractionIntent.For(MouseButton.Right, tool));
        Assert.Equal(paintMode, InteractionIntent.PaintMode(tool));
    }

    [Fact]
    public void EmptyHandsProduceNoEdge()
    {
        Assert.Equal(InteractionKind.None, InteractionIntent.For(MouseButton.Left, null));
        Assert.Equal(InteractionKind.None, InteractionIntent.For(MouseButton.Right, null));
        Assert.Null(InteractionIntent.PaintMode(null));
    }

    [Fact]
    public void AnItemWithoutAPlaceableOrToolProducesNoEdge()
    {
        var item = new Item("wb:relic", "Relic", iconID: null, maxStack: 1, placeable: null, tool: null, viewModel: null, worldModel: null);

        Assert.Equal(InteractionKind.None, InteractionIntent.For(MouseButton.Left, item));
        Assert.Equal(InteractionKind.None, InteractionIntent.For(MouseButton.Right, item));
    }

    [Fact]
    public void TheMiddleButtonProducesNoEdge()
    {
        Item laser = Tool("laser", ToolType.Mining);

        Assert.Equal(InteractionKind.None, InteractionIntent.For(MouseButton.Middle, laser));
    }

    private static Item Placeable(string brickID)
    {
        var placeable = new PlaceableDefinition { Type = PlaceableType.Brick, ID = brickID };
        return new Item(brickID, "Block", iconID: null, maxStack: 100, placeable, tool: null, viewModel: null, worldModel: null);
    }

    private static Item Tool(string itemID, ToolType toolType)
    {
        var tool = new ToolDefinition { Type = toolType };
        return new Item(itemID, "Tool", iconID: null, maxStack: 1, placeable: null, tool, viewModel: null, worldModel: null);
    }
}
