#nullable enable
using System;
using System.Numerics;
using Xunit;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;

namespace Swordfish.Tests;

/// <summary>
/// Pins the interaction handler chain: filters select handlers, a reject short-circuits, and each
/// handler sees the running resolution so an override carries forward.
/// </summary>
public class InteractionHandlerRegistryTests
{
    private static readonly InteractionResolution BASE = Resolution(InteractionAction.Break, entity: 1);

    [Fact]
    public void AppliesMatchingHandlersInRegistrationOrder()
    {
        var registry = new InteractionHandlerRegistry();
        registry.Register(new Handler(default, _ => Resolution(InteractionAction.Place, entity: 2)));
        registry.Register(new Handler(default, _ => Resolution(InteractionAction.Break, entity: 3)));

        InteractionResolution resolution = registry.Apply(Request(), BASE, heldItemID: null);

        Assert.Equal(InteractionAction.Break, resolution.Action);
        Assert.Equal(3, resolution.Entity);
    }

    [Fact]
    public void RejectsAndShortCircuitsWhenAHandlerReturnsNone()
    {
        var registry = new InteractionHandlerRegistry();
        var reached = new Reached();

        registry.Register(new Handler(default, _ => InteractionResolution.None));
        registry.Register(new Handler(default, _ =>
        {
            reached.Value = true;
            return Resolution(InteractionAction.Place, entity: 2);
        }));

        InteractionResolution resolution = registry.Apply(Request(), BASE, heldItemID: null);

        Assert.Equal(InteractionAction.None, resolution.Action);
        Assert.False(reached.Value, "A rejecting handler must short-circuit the chain.");
    }

    [Fact]
    public void ALaterAllowKeepsAnEarlierOverride()
    {
        var registry = new InteractionHandlerRegistry();
        registry.Register(new Handler(default, _ => Resolution(InteractionAction.Place, entity: 2)));

        //  "Allow" returns the context's resolution, which must be the earlier override, not the base.
        registry.Register(new Handler(default, context => context.Resolution));

        InteractionResolution resolution = registry.Apply(Request(), BASE, heldItemID: null);

        Assert.Equal(InteractionAction.Place, resolution.Action);
        Assert.Equal(2, resolution.Entity);
    }

    [Fact]
    public void SkipsHandlersWhoseFilterDoesNotMatch()
    {
        var registry = new InteractionHandlerRegistry();
        var reached = new Reached();

        registry.Register(new Handler(new InteractionHandlerFilter(HeldItemID: "other"), _ =>
        {
            reached.Value = true;
            return Resolution(InteractionAction.Place, entity: 2);
        }));

        InteractionResolution resolution = registry.Apply(Request(), BASE, heldItemID: "wb:panel");

        Assert.False(reached.Value);
        Assert.Equal(InteractionAction.Break, resolution.Action);
    }

    private static InteractionRequest Request()
    {
        return new InteractionRequest(
            Vector3.Zero,
            null,
            InteractionKind.PrimaryPressed,
            null,
            GameMode.Creative,
            reach: 9.5f
        );
    }

    private static InteractionResolution Resolution(InteractionAction action, int entity)
    {
        return new InteractionResolution(action, entity, default, default);
    }

    private sealed class Reached
    {
        public bool Value;
    }

    private sealed class Handler(InteractionHandlerFilter filter, Func<InteractionContext, InteractionResolution> handle) : IInteractionHandler
    {
        public InteractionHandlerFilter Filter => filter;

        public InteractionResolution Handle(in InteractionContext context)
        {
            return handle(context);
        }
    }
}
