#nullable enable
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>
/// Pins the interaction control scheme: Primary is the main edge for every item that has a primary action,
/// only a modifier tool binds Secondary to clear its paint, and an item with no action for an input
/// produces no edge.
/// </summary>
public class InteractionIntentTests
{
    [Fact]
    public void APlaceableItemPressesThePrimaryEdgeOnPrimaryInput()
    {
        Item panel = Placeable("wb:panel");

        Assert.Equal(InteractionKind.PrimaryPressed, InteractionIntent.For(InteractionInput.Primary, panel));
        Assert.Equal(InteractionKind.None, InteractionIntent.For(InteractionInput.Secondary, panel));
        Assert.Null(InteractionIntent.PaintMode(panel));
    }

    [Fact]
    public void ARemovalToolPressesThePrimaryEdgeOnPrimaryInput()
    {
        Item laser = Tool("laser", ToolType.Mining);

        Assert.Equal(InteractionKind.PrimaryPressed, InteractionIntent.For(InteractionInput.Primary, laser));
        Assert.Equal(InteractionKind.None, InteractionIntent.For(InteractionInput.Secondary, laser));
    }

    [Theory]
    [InlineData(ToolType.Sprayer, PaintKind.Color)]
    [InlineData(ToolType.Drill, PaintKind.Texture)]
    public void AModifierToolPressesPrimaryAndSecondaryEdges(ToolType toolType, PaintKind paintMode)
    {
        Item tool = Tool("wb:modifier", toolType);

        Assert.Equal(InteractionKind.PrimaryPressed, InteractionIntent.For(InteractionInput.Primary, tool));
        Assert.Equal(InteractionKind.SecondaryPressed, InteractionIntent.For(InteractionInput.Secondary, tool));
        Assert.Equal(paintMode, InteractionIntent.PaintMode(tool));
    }

    [Fact]
    public void AConsumableBindsSecondaryAndDoesNotBindPrimary()
    {
        var item = new Item(
            "wb:medkit",
            "Medkit",
            iconID: null,
            maxStack: 10,
            placeable: null,
            tool: null,
            viewModel: null,
            worldModel: null,
            consumable: new ConsumableDefinition { HealHp = 40f });

        Assert.Equal(InteractionKind.None, InteractionIntent.For(InteractionInput.Primary, item));
        Assert.Equal(InteractionKind.SecondaryPressed, InteractionIntent.For(InteractionInput.Secondary, item));
    }

    [Fact]
    public void EmptyHandsProduceNoEdge()
    {
        Assert.Equal(InteractionKind.None, InteractionIntent.For(InteractionInput.Primary, null));
        Assert.Equal(InteractionKind.None, InteractionIntent.For(InteractionInput.Secondary, null));
        Assert.Null(InteractionIntent.PaintMode(null));
    }

    [Fact]
    public void AnItemWithoutAPlaceableOrToolProducesNoEdge()
    {
        var item = new Item("wb:relic", "Relic", iconID: null, maxStack: 1, placeable: null, tool: null, viewModel: null, worldModel: null);

        Assert.Equal(InteractionKind.None, InteractionIntent.For(InteractionInput.Primary, item));
        Assert.Equal(InteractionKind.None, InteractionIntent.For(InteractionInput.Secondary, item));
    }

    [Fact]
    public void NoneInputProducesNoEdge()
    {
        Item laser = Tool("laser", ToolType.Mining);

        Assert.Equal(InteractionKind.None, InteractionIntent.For(InteractionInput.None, laser));
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
