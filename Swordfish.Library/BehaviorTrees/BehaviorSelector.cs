using System.Collections.Generic;

namespace Swordfish.Library.BehaviorTrees;

public sealed class BehaviorSelector : BehaviorNode, IBehaviorCompositor
{
    public BehaviorSelector(BehaviorNode child) : base(child) { }

    public BehaviorSelector(params BehaviorNode[] children) : base(children) { }

    public BehaviorSelector(IEnumerable<BehaviorNode> children) : base(children) { }

    public override BehaviorState Evaluate(object target, float delta)
    {
        for (var i = 0; i < Children.Count; i++)
        {
            BehaviorState state = Children[i].Evaluate(target, delta);

            if (state != BehaviorState.FAILED)
            {
                return state;
            }
        }

        return BehaviorState.FAILED;
    }
}