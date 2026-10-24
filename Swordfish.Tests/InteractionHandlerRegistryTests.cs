#nullable enable
using System;
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
/// Pins the staged interaction pipeline: priority bands, per-stage filters, stage-scoped ownership
/// (<see cref="InteractionDecision.Handled"/>), and the two pipeline-scoped stops.
/// </summary>
public class InteractionHandlerRegistryTests
{
    private static readonly InteractionResolution BASE = Resolution(InteractionAction.Break, entity: 1);

    [Fact]
    public void RunsHandlersInARegistrationOrderWithinABand()
    {
        var registry = new InteractionHandlerRegistry();
        var order = new Order();

        registry.Register(InteractionStage.Validate, InteractionPriority.Normal, new Handler(order, "first", _ => InteractionDecision.Continue));
        registry.Register(InteractionStage.Validate, InteractionPriority.Normal, new Handler(order, "second", _ => InteractionDecision.Continue));

        Run(registry, BASE);

        Assert.Equal("first,second", order.Value);
    }

    [Fact]
    public void RunsPriorityBandsHighToLow()
    {
        var registry = new InteractionHandlerRegistry();
        var order = new Order();

        registry.Register(InteractionStage.Validate, InteractionPriority.Low, new Handler(order, "low", _ => InteractionDecision.Continue));
        registry.Register(InteractionStage.Validate, InteractionPriority.High, new Handler(order, "high", _ => InteractionDecision.Continue));
        registry.Register(InteractionStage.Validate, InteractionPriority.Normal, new Handler(order, "normal", _ => InteractionDecision.Continue));

        Run(registry, BASE);

        Assert.Equal("high,normal,low", order.Value);
    }

    [Fact]
    public void HandledSkipsTheRestOfTheStageButRunsTheNext()
    {
        var registry = new InteractionHandlerRegistry();
        var order = new Order();

        registry.Register(InteractionStage.Validate, InteractionPriority.High, new Handler(order, "validate-high", _ => InteractionDecision.Continue));
        registry.Register(InteractionStage.Validate, InteractionPriority.Normal, new Handler(order, "validate-normal", _ => InteractionDecision.Handled));
        registry.Register(InteractionStage.Validate, InteractionPriority.Low, new Handler(order, "validate-low", _ => InteractionDecision.Continue));
        registry.Register(InteractionStage.Apply, InteractionPriority.Normal, new Handler(order, "apply", _ => InteractionDecision.Continue));

        InteractionRunResult result = Run(registry, BASE);

        Assert.Equal("validate-high,validate-normal,apply", order.Value);
        Assert.Equal(InteractionDecision.Continue, result.Decision);
    }

    [Fact]
    public void ConsumeStopsEveryLaterStage()
    {
        var registry = new InteractionHandlerRegistry();
        var order = new Order();

        registry.Register(InteractionStage.Apply, InteractionPriority.Normal, new Handler(order, "apply", _ => InteractionDecision.Consume));
        registry.Register(InteractionStage.React, InteractionPriority.Normal, new Handler(order, "react", _ => InteractionDecision.Continue));

        InteractionRunResult result = Run(registry, BASE);

        Assert.Equal("apply", order.Value);
        Assert.Equal(InteractionDecision.Consume, result.Decision);
    }

    [Fact]
    public void CancelStopsEveryLaterStage()
    {
        var registry = new InteractionHandlerRegistry();
        var order = new Order();

        registry.Register(InteractionStage.Resolve, InteractionPriority.Normal, new Handler(order, "resolve", _ => InteractionDecision.Cancel));
        registry.Register(InteractionStage.Apply, InteractionPriority.Normal, new Handler(order, "apply", _ => InteractionDecision.Continue));

        InteractionRunResult result = Run(registry, BASE);

        Assert.Equal("resolve", order.Value);
        Assert.Equal(InteractionDecision.Cancel, result.Decision);
    }

    [Fact]
    public void SkipsHandlersWhoseFilterDoesNotMatch()
    {
        var registry = new InteractionHandlerRegistry();
        var reached = new Order();

        registry.Register(
            InteractionStage.Validate,
            InteractionPriority.Normal,
            new Handler(
                reached,
                "mismatched",
                _ => InteractionDecision.Continue,
                new InteractionHandlerFilter(HeldItemID: "other")
            )
        );

        Run(registry, BASE, heldItemID: "wb:panel");

        Assert.Equal(string.Empty, reached.Value);
    }

    [Fact]
    public void AHandlerSeesAndAdjustsTheRunningResolution()
    {
        var registry = new InteractionHandlerRegistry();

        registry.Register(InteractionStage.Validate, InteractionPriority.High, new AdjustingHandler(InteractionAction.Place, entity: 2));
        registry.Register(
            InteractionStage.Validate,
            InteractionPriority.Normal,
            new Handler(new Order(), "reads", context =>
            {
                Assert.Equal(InteractionAction.Place, context.Resolution.Action);
                Assert.Equal(2, context.Resolution.Entity);
                return InteractionDecision.Continue;
            })
        );

        InteractionRunResult result = Run(registry, BASE);

        Assert.Equal(InteractionAction.Place, result.Resolution.Action);
        Assert.Equal(2, result.Resolution.Entity);
    }

    [Fact]
    public void TheDefaultResolveHandlerResolvesTheHint()
    {
        var registry = new InteractionHandlerRegistry();
        registry.Register(InteractionStage.Resolve, InteractionPriority.Normal, new HintResolveHandler(new FakeRegistry()));

        var context = Context(Hint(cell: 0));
        InteractionRunResult result = registry.Run(ref context);

        Assert.Equal(InteractionAction.Break, result.Resolution.Action);
    }

    [Fact]
    public void TheDefaultResolveHandlerLeavesAnUnresolvedHintForLaterHandlers()
    {
        var registry = new InteractionHandlerRegistry();
        var order = new Order();

        registry.Register(InteractionStage.Resolve, InteractionPriority.Normal, new HintResolveHandler(new FakeRegistry()));
        registry.Register(InteractionStage.Resolve, InteractionPriority.Low, new Handler(order, "later", _ => InteractionDecision.Handled));

        //  A hintless request cannot be resolved, so the stage must stay open for the later handler.
        Run(registry, hint: null, in InteractionResolution.None);

        Assert.Equal("later", order.Value);
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
        Func<InteractionContext, InteractionDecision> handle,
        InteractionHandlerFilter? filter = null
    ) : IInteractionHandler
    {
        public InteractionHandlerFilter Filter => filter ?? default;

        public InteractionDecision Handle(ref InteractionContext context)
        {
            order.Add(name);
            return handle(context);
        }
    }

    private sealed class AdjustingHandler(InteractionAction action, int entity) : IInteractionHandler
    {
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
