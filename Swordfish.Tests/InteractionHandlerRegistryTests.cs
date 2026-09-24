using System;
using System.Numerics;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Phase 6.1 acceptance, headless. The server modding API: a public, server-side registry of
/// interaction handlers. Handlers register against an (interaction kind, held item, game mode) key and,
/// after base validation, may reject (return <see cref="InteractionResolution.None"/>), pass through
/// (return the context's base resolution), or override/augment (return a different resolution).
/// </summary>
public class InteractionHandlerRegistryTests
{
    [Fact]
    public void HandlerMayRejectAnInteraction()
    {
        IInteractionHandlerRegistry registry = new InteractionHandlerRegistry();
        registry.Register(new ActionHandler(new InteractionHandlerFilter(), ctx => InteractionResolution.None));

        InteractionResolution result = registry.Apply(Request(), BreakResolution(), heldItemID: "laser");

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void HandlerMayOverrideAnInteraction()
    {
        IInteractionHandlerRegistry registry = new InteractionHandlerRegistry();
        InteractionResolution overridden = new(InteractionAction.Place, entity: 42, Coordinate(7, 7, 7), Voxel(9));
        registry.Register(new ActionHandler(new InteractionHandlerFilter(), ctx => overridden));

        InteractionResolution result = registry.Apply(Request(), BreakResolution(), heldItemID: "laser");

        Assert.Equal(InteractionAction.Place, result.Action);
        Assert.Equal(42, result.Entity);
        Assert.Equal(Coordinate(7, 7, 7), result.Coordinate);
        Assert.Equal((ushort)9, result.Voxel.ID);
    }

    [Fact]
    public void NoMatchingHandlerLeavesBaseResolutionUntouched()
    {
        IInteractionHandlerRegistry registry = new InteractionHandlerRegistry();
        //  Kind filter for a break; the interaction is a place, so the handler never runs.
        registry.Register(new ActionHandler(
            new InteractionHandlerFilter(Kind: InteractionKind.PrimaryPressed, HeldItemID: "laser"),
            ctx => InteractionResolution.None
        ));

        InteractionResolution result = registry.Apply(
            Request(kind: InteractionKind.SecondaryPressed, heldItemID: "panel"),
            PlaceResolution(),
            heldItemID: "panel"
        );

        Assert.Equal(InteractionAction.Place, result.Action);
    }

    [Fact]
    public void HeldItemAndGameModeKeysFilterWhichHandlerRuns()
    {
        IInteractionHandlerRegistry registry = new InteractionHandlerRegistry();
        registry.Register(new ActionHandler(
            new InteractionHandlerFilter(HeldItemID: "laser", GameMode: GameMode.Adventure),
            ctx => InteractionResolution.None
        ));

        //  Matches: held "laser" in adventure.
        Assert.Equal(
            InteractionAction.None,
            registry.Apply(Request(heldItemID: "laser", gameMode: GameMode.Adventure), BreakResolution(), "laser").Action
        );

        //  Wrong held item: no match, base resolution preserved.
        Assert.Equal(
            InteractionAction.Break,
            registry.Apply(Request(heldItemID: "panel", gameMode: GameMode.Adventure), BreakResolution(), "panel").Action
        );

        //  Wrong mode: no match, base resolution preserved.
        Assert.Equal(
            InteractionAction.Break,
            registry.Apply(Request(heldItemID: "laser", gameMode: GameMode.Creative), BreakResolution(), "laser").Action
        );
    }

    [Fact]
    public void LastMatchingHandlerInRegistrationOrderWins()
    {
        IInteractionHandlerRegistry registry = new InteractionHandlerRegistry();
        InteractionResolution overrideBreak = new(InteractionAction.Place, entity: 1, Coordinate(1, 1, 1), Voxel(1));
        InteractionResolution overridePlace = new(InteractionAction.Place, entity: 2, Coordinate(2, 2, 2), Voxel(2));
        registry.Register(new ActionHandler(new InteractionHandlerFilter(), ctx => overrideBreak));
        registry.Register(new ActionHandler(new InteractionHandlerFilter(), ctx => overridePlace));

        InteractionResolution result = registry.Apply(Request(), BreakResolution(), heldItemID: "laser");

        Assert.Equal(2, result.Entity);
        Assert.Equal(Coordinate(2, 2, 2), result.Coordinate);
        Assert.Equal((ushort)2, result.Voxel.ID);
    }

    [Fact]
    public void HandlerReceivesTheCellContext()
    {
        IInteractionHandlerRegistry registry = new InteractionHandlerRegistry();
        InteractionContext? seen = null;
        registry.Register(new ActionHandler(new InteractionHandlerFilter(), context =>
        {
            seen = context;
            return context.Resolution;
        }));

        InteractionResolution baseResolution = BreakResolution();
        registry.Apply(Request(heldItemID: "laser"), baseResolution, heldItemID: "laser");

        Assert.NotNull(seen);
        Assert.Equal(5, seen.Value.Entity);
        Assert.Equal(Coordinate(1, 2, 3), seen.Value.Coordinate);
        Assert.Equal((ushort)7, seen.Value.CurrentVoxel.ID);
        Assert.Equal("laser", seen.Value.HeldItemID);
        Assert.Equal(InteractionKind.PrimaryPressed, seen.Value.Request.Kind);
        Assert.Equal(GameMode.Adventure, seen.Value.Request.GameMode);
        Assert.Equal(InteractionAction.Break, seen.Value.Resolution.Action);
    }

    private static InteractionRequest Request(
        InteractionKind kind = InteractionKind.PrimaryPressed,
        string heldItemID = "laser",
        GameMode gameMode = GameMode.Adventure
    ) {
        _ = heldItemID;
        return new InteractionRequest(Vector3.Zero, null, kind, null, gameMode, 9.5f);
    }

    private static InteractionResolution BreakResolution()
    {
        return new InteractionResolution(InteractionAction.Break, entity: 5, Coordinate(1, 2, 3), Voxel(7));
    }

    private static InteractionResolution PlaceResolution()
    {
        return new InteractionResolution(InteractionAction.Place, entity: 5, Coordinate(1, 2, 3), Voxel(7));
    }

    private static Int3 Coordinate(int x, int y, int z) => new(x, y, z);

    private static Voxel Voxel(ushort id) => new(id, 0, 0);

    private sealed class ActionHandler(InteractionHandlerFilter filter, Func<InteractionContext, InteractionResolution> handle) : IInteractionHandler
    {
        public InteractionHandlerFilter Filter { get; } = filter;

        public InteractionResolution Handle(in InteractionContext context)
        {
            return handle(context);
        }
    }
}