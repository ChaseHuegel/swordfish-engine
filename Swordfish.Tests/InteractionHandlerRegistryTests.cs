#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Voxels;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Pins the staged interaction pipeline: handlers place themselves by stage and priority, filters run
/// per stage, <see cref="InteractionDecision.Handled"/> owns a stage, and the two pipeline-scoped stops.
/// </summary>
public class InteractionHandlerRegistryTests
{
    private static readonly InteractionResolution BASE = Resolution(InteractionAction.Break, entity: 1);

    [Fact]
    public void RunsHandlersInRegistrationOrderWithinABand()
    {
        var order = new Order();
        var registry = new InteractionHandlerRegistry(
        [
            new Handler(order, "first", InteractionStage.Validate, InteractionPriority.Normal),
            new Handler(order, "second", InteractionStage.Validate, InteractionPriority.Normal),
        ]);

        Run(registry, BASE);

        Assert.Equal("first,second", order.Value);
    }

    [Fact]
    public void RunsPriorityBandsHighToLow()
    {
        var order = new Order();
        var registry = new InteractionHandlerRegistry(
        [
            new Handler(order, "low", InteractionStage.Validate, InteractionPriority.Low),
            new Handler(order, "high", InteractionStage.Validate, InteractionPriority.High),
            new Handler(order, "normal", InteractionStage.Validate, InteractionPriority.Normal),
        ]);

        Run(registry, BASE);

        Assert.Equal("high,normal,low", order.Value);
    }

    [Fact]
    public void HandledSkipsTheRestOfTheStageButRunsTheNext()
    {
        var order = new Order();
        var registry = new InteractionHandlerRegistry(
        [
            new Handler(order, "validate-high", InteractionStage.Validate, InteractionPriority.High),
            new Handler(order, "validate-normal", InteractionStage.Validate, InteractionPriority.Normal, _ => InteractionDecision.Handled),
            new Handler(order, "validate-low", InteractionStage.Validate, InteractionPriority.Low),
            new Handler(order, "commit", InteractionStage.Commit, InteractionPriority.Normal),
        ]);

        InteractionRunResult result = Run(registry, BASE);

        Assert.Equal("validate-high,validate-normal,commit", order.Value);
        Assert.Equal(InteractionDecision.Continue, result.Decision);
    }

    [Fact]
    public void ConsumeStopsEveryLaterStage()
    {
        var order = new Order();
        var registry = new InteractionHandlerRegistry(
        [
            new Handler(order, "commit", InteractionStage.Commit, InteractionPriority.Normal, _ => InteractionDecision.Consume),
            new Handler(order, "react", InteractionStage.React, InteractionPriority.Normal),
        ]);

        InteractionRunResult result = Run(registry, BASE);

        Assert.Equal("commit", order.Value);
        Assert.Equal(InteractionDecision.Consume, result.Decision);
    }

    [Fact]
    public void CancelStopsEveryLaterStage()
    {
        var order = new Order();
        var registry = new InteractionHandlerRegistry(
        [
            new Handler(order, "resolve", InteractionStage.Resolve, InteractionPriority.Normal, _ => InteractionDecision.Cancel),
            new Handler(order, "commit", InteractionStage.Commit, InteractionPriority.Normal),
        ]);

        InteractionRunResult result = Run(registry, BASE);

        Assert.Equal("resolve", order.Value);
        Assert.Equal(InteractionDecision.Cancel, result.Decision);
    }

    [Fact]
    public void SkipsHandlersWhoseFilterDoesNotMatch()
    {
        var reached = new Order();
        var registry = new InteractionHandlerRegistry(
        [
            new Handler(
                reached,
                "mismatched",
                InteractionStage.Validate,
                InteractionPriority.Normal,
                filter: new InteractionHandlerFilter(HeldItemID: "other")),
        ]);

        Run(registry, BASE, heldItemID: "wb:panel");

        Assert.Equal(string.Empty, reached.Value);
    }

    [Fact]
    public void AHandlerSeesAndAdjustsTheRunningResolution()
    {
        var registry = new InteractionHandlerRegistry(
        [
            new AdjustingHandler(InteractionAction.Place, entity: 2),
            new Handler(
                new Order(),
                "reads",
                InteractionStage.Validate,
                InteractionPriority.Normal,
                context =>
                {
                    Assert.Equal(InteractionAction.Place, context.Resolution.Action);
                    Assert.Equal(2, context.Resolution.Entity);
                    return InteractionDecision.Continue;
                }),
        ]);

        InteractionRunResult result = Run(registry, BASE);

        Assert.Equal(InteractionAction.Place, result.Resolution.Action);
        Assert.Equal(2, result.Resolution.Entity);
    }

    [Fact]
    public void TheDefaultResolveHandlerResolvesTheHint()
    {
        var registry = new InteractionHandlerRegistry([new HintResolver(new FakeRegistry())]);

        var context = Context(Hint(cell: 0));
        InteractionRunResult result = registry.Run(ref context);

        Assert.Equal(InteractionAction.Break, result.Resolution.Action);
    }

    [Fact]
    public void TheDefaultResolveHandlerLeavesAnUnresolvedHintForLaterHandlers()
    {
        var order = new Order();
        var registry = new InteractionHandlerRegistry(
        [
            new HintResolver(new FakeRegistry()),
            new Handler(order, "later", InteractionStage.Resolve, InteractionPriority.Low, _ => InteractionDecision.Handled),
        ]);

        //  A hintless request cannot be resolved, so the stage must stay open for the later handler.
        Run(registry, hint: null, in InteractionResolution.None);

        Assert.Equal("later", order.Value);
    }

    [Fact]
    public void AnUndefinedStageIsRejectedWhenTheRegistryIsBuilt()
    {
        var handlers = new IInteractionHandler[]
        {
            new Handler(new Order(), "bad", (InteractionStage)99, InteractionPriority.Normal),
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new InteractionHandlerRegistry(handlers));
    }

    [Fact]
    public void GetHandlersReturnsTheStageOrder()
    {
        var registry = new InteractionHandlerRegistry(
        [
            new Handler(new Order(), "normal", InteractionStage.React, InteractionPriority.Normal),
            new Handler(new Order(), "high", InteractionStage.React, InteractionPriority.High),
        ]);

        IReadOnlyList<IInteractionHandler> handlers = registry.GetHandlers(InteractionStage.React);

        Assert.Equal(2, handlers.Count);
        Assert.Equal(InteractionPriority.High, handlers[0].Priority);
        Assert.Equal(InteractionPriority.Normal, handlers[1].Priority);
    }

    private static InteractionRunResult Run(
        InteractionHandlerRegistry registry,
        in InteractionResolution resolution,
        string? heldItemID = null
    ) {
        return Run(registry, Hint(cell: 0), in resolution, heldItemID);
    }

    private static InteractionRunResult Run(
        InteractionHandlerRegistry registry,
        BrickInteraction? hint,
        in InteractionResolution resolution,
        string? heldItemID = null
    ) {
        var context = Context(hint, heldItemID);
        context.Resolution = resolution;
        return registry.Run(ref context);
    }

    private static InteractionContext Context(BrickInteraction? hint, string? heldItemID = null)
    {
        var store = new DataStore();
        var request = new InteractionRequest(
            Vector3.Zero,
            hint,
            hint == null ? InteractionKind.None : InteractionKind.PrimaryPressed,
            null,
            GameMode.Creative,
            9.5f
        );

        return new InteractionContext(
            store,
            player: 1,
            Uuid.FromValue(1),
            sequence: 7,
            new FakeWorld(FilledVoxels()),
            in request,
            heldItemID
        );
    }

    private static BrickInteraction Hint(int cell)
    {
        return new BrickInteraction
        {
            TargetEntity = Uuid.FromValue(1).ToValue(),
            TargetX = cell,
            TargetY = 0,
            TargetZ = 0,
            HintShape = 0,
            HintOrientation = 0,
        };
    }

    private static VoxelObject FilledVoxels()
    {
        var voxels = new VoxelObject(4);
        voxels.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Block, lightLevel: 0), default));
        return voxels;
    }

    private static InteractionResolution Resolution(InteractionAction action, int entity)
    {
        return new InteractionResolution(action, entity, default, default);
    }

    private sealed class Order
    {
        public string Value = string.Empty;

        public void Add(string name)
        {
            Value = Value.Length == 0 ? name : $"{Value},{name}";
        }
    }

    private sealed class Handler(
        Order order,
        string name,
        InteractionStage stage,
        InteractionPriority priority,
        Func<InteractionContext, InteractionDecision>? handle = null,
        InteractionHandlerFilter? filter = null
    ) : IInteractionHandler
    {
        public InteractionStage Stage => stage;

        public InteractionPriority Priority => priority;

        public InteractionHandlerFilter Filter => filter ?? default;

        public InteractionDecision Handle(ref InteractionContext context)
        {
            order.Add(name);
            return handle?.Invoke(context) ?? InteractionDecision.Continue;
        }
    }

    private sealed class AdjustingHandler(InteractionAction action, int entity) : IInteractionHandler
    {
        public InteractionStage Stage => InteractionStage.Validate;

        public InteractionPriority Priority => InteractionPriority.High;

        public InteractionHandlerFilter Filter => default;

        public InteractionDecision Handle(ref InteractionContext context)
        {
            context.Resolution = new InteractionResolution(action, entity, context.Resolution.Coordinate, context.Resolution.Voxel);
            return InteractionDecision.Continue;
        }
    }

    private sealed class FakeWorld : IVoxelInteractionWorld
    {
        private readonly VoxelObject _voxels;

        public FakeWorld(VoxelObject voxels)
        {
            _voxels = voxels;
        }

        public Result<VoxelTarget> GetTarget(in Uuid entityUuid)
        {
            return Result<VoxelTarget>.FromSuccess(new VoxelTarget(1, _voxels, new TransformComponent()));
        }
    }

    private sealed class FakeRegistry : IBrickRegistry
    {
        public ushort Id(string name) => name == "wb:panel" ? (ushort)1 : (ushort)0;

        public string? Name(ushort id) => id == 1 ? "wb:panel" : null;
    }
}
