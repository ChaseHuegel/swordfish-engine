using System;
using System.Collections.Generic;
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace Swordfish.Library.BehaviorTrees;

public abstract class BehaviorNode
{
    public readonly List<BehaviorNode> Children;

    public BehaviorNode(params BehaviorNode[] children)
    {
        Children = [..children];
    }

    public BehaviorNode(IEnumerable<BehaviorNode> children)
    {
        Children = [..children];
    }

    public abstract BehaviorState Evaluate(object target, float delta);
}

public abstract class BehaviorNode<TTarget> : BehaviorNode where TTarget : class
{
    public BehaviorNode(params BehaviorNode[] children) : base(children) { }

    public BehaviorNode(IEnumerable<BehaviorNode> children) : base(children) { }

    public override BehaviorState Evaluate(object target, float delta) => Evaluate((TTarget)target, delta);

    public abstract BehaviorState Evaluate(TTarget target, float delta);
}

public class BehaviorDynamic<TTarget> : BehaviorNode<TTarget>, IBehaviorAction
    where TTarget : class
{
    private readonly Func<TTarget, float, BehaviorState> _func;

    public BehaviorDynamic(Func<TTarget, float, BehaviorState> func)
    {
        _func = func;
    }

    public override BehaviorState Evaluate(TTarget target, float delta)
    {
        return _func(target, delta);
    }
}