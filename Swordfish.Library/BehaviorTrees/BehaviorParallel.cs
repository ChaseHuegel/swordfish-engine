using System.Collections.Generic;

namespace Swordfish.Library.BehaviorTrees;

// ReSharper disable once UnusedType.Global
public sealed class BehaviorParallel : BehaviorNode, IBehaviorCompositor
{
    public BehaviorParallel(BehaviorNode child) : base(child) { }

    public BehaviorParallel(params BehaviorNode[] children) : base(children) { }

    public BehaviorParallel(IEnumerable<BehaviorNode> children) : base(children) { }

    public override BehaviorState Evaluate(object target, float delta)
    {
        var running = false;
        var anySuccess = false;

        for (var i = 0; i < Children.Count; i++)
        {
            BehaviorState state = Children[i].Evaluate(target, delta);

            if (!anySuccess && state == BehaviorState.SUCCESS)
            {
                anySuccess = true;
            }
            else if (!running && state == BehaviorState.RUNNING)
            {
                running = true;
            }
        }

        if (!anySuccess)
        {
            return BehaviorState.FAILED;
        }

        return running ? BehaviorState.RUNNING : BehaviorState.SUCCESS;
    }
}