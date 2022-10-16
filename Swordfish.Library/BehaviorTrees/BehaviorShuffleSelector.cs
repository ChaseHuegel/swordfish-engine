using System;
using System.Collections.Generic;
using Swordfish.Library.Util;

namespace Swordfish.Library.BehaviorTrees
{
    public sealed class BehaviorShuffleSelector : BehaviorNode
    {
        public BehaviorShuffleSelector(BehaviorNode child) : base(child) { }

        public BehaviorShuffleSelector(params BehaviorNode[] children) : base(children) { }

        public BehaviorShuffleSelector(IEnumerable<BehaviorNode> children) : base(children) { }

        public override BehaviorState Evaluate(object target, float delta)
        {
            int offset = MathS.Random.Next(Children.Count);
            for (int i = 0; i < Children.Count; i++)
            {
                int offsetIndex = i + offset;
                if (offsetIndex >= Children.Count)
                    offsetIndex -= Children.Count;

                BehaviorState state = Children[offsetIndex].Evaluate(target, delta);

                if (state != BehaviorState.FAILED)
                    return state;
            }

            return BehaviorState.FAILED;
        }
    }
}
