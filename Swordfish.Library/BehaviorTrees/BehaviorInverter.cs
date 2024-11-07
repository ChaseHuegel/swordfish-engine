using System;

namespace Swordfish.Library.BehaviorTrees;

// ReSharper disable once UnusedType.Global
public sealed class BehaviorInverter(in BehaviorNode child) : BehaviorNode(child), IBehaviorDecorator
{
    public override BehaviorState Evaluate(object target, float delta)
    {
        return Children[0].Evaluate(target, delta) switch
        {
            BehaviorState.RUNNING => BehaviorState.RUNNING,
            BehaviorState.SUCCESS => BehaviorState.FAILED,
            BehaviorState.FAILED => BehaviorState.SUCCESS,
            _ => throw new NotImplementedException(),
        };
    }
}