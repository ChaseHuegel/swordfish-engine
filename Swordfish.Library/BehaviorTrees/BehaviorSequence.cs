using System;
using System.Collections.Generic;

namespace Swordfish.Library.BehaviorTrees
{
    public sealed class BehaviorSequence : BehaviorNode
    {
        public BehaviorSequence(BehaviorNode child) : base(child) { }

        public BehaviorSequence(params BehaviorNode[] children) : base(children) { }

        public BehaviorSequence(IEnumerable<BehaviorNode> children) : base(children) { }

        public override BehaviorState Evaluate(object target, float delta)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                BehaviorState state = Children[i].Evaluate(target, delta);

                if (state != BehaviorState.SUCCESS)
                    return state;
            }

            return BehaviorState.SUCCESS;
        }
    }
}
