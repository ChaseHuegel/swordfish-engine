#nullable enable
using Krill;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;
using WaywardBeyond.Server.Interactions;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>
/// Pins the validate-stage rule that authorizes a break: only a removal tool held in the active slot may
/// break a brick, so bare hands and block items cannot. A resolution that is not a break passes untouched.
/// </summary>
public class BreakToolValidatorTests
{
    [Fact]
    public void ARemovalToolAdmitsTheBreak()
    {
        var validator = new BreakToolValidator(new StubItemDatabase(Tool("laser", ToolType.Mining)));
        InteractionContext context = Context(heldItemID: "laser");

        Assert.Equal(InteractionDecision.Continue, validator.Handle(ref context));
    }

    [Fact]
    public void BareHandsRejectTheBreak()
    {
        var validator = new BreakToolValidator(new StubItemDatabase());
        InteractionContext context = Context(heldItemID: null);

        Assert.Equal(InteractionDecision.Cancel, validator.Handle(ref context));
    }

    [Fact]
    public void APlaceableItemRejectsTheBreak()
    {
        var validator = new BreakToolValidator(new StubItemDatabase(Placeable("wb:panel")));
        InteractionContext context = Context(heldItemID: "wb:panel");

        Assert.Equal(InteractionDecision.Cancel, validator.Handle(ref context));
    }

    [Fact]
    public void AModifierToolRejectsTheBreak()
    {
        var validator = new BreakToolValidator(new StubItemDatabase(Tool("wb:sprayer", ToolType.Sprayer)));
        InteractionContext context = Context(heldItemID: "wb:sprayer");

        Assert.Equal(InteractionDecision.Cancel, validator.Handle(ref context));
    }

    [Fact]
    public void ANonBreakResolutionPassesUntouched()
    {
        var validator = new BreakToolValidator(new StubItemDatabase());
        InteractionContext context = Context(heldItemID: null, action: InteractionAction.Place);

        Assert.Equal(InteractionDecision.Continue, validator.Handle(ref context));
    }

    private static InteractionContext Context(string? heldItemID, InteractionAction action = InteractionAction.Break)
    {
        var request = new InteractionRequest(
            default,
            new BrickInteraction(),
            InteractionKind.PrimaryPressed,
            null,
            GameMode.Creative,
            reach: 9.5f
        );

        var context = new InteractionContext(
            new DataStore(),
            player: 0,
            Uuid.Null,
            sequence: 1,
            world: null!,
            in request,
            heldItemID
        );
        context.Resolution = new InteractionResolution(action, entity: 0, coordinate: default, voxel: new Voxel(1, default, default));
        return context;
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

    private sealed class StubItemDatabase(params Item[] items) : IItemDatabase
    {
        public Result<Item> Get(string id)
        {
            foreach (Item item in items)
            {
                if (item.ID == id)
                {
                    return Result<Item>.FromSuccess(item);
                }
            }

            return Result<Item>.FromFailure((string?)null);
        }
    }
}
