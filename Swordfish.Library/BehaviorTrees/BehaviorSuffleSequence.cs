using System.Collections.Generic;
using Swordfish.Library.Util;

namespace Swordfish.Library.BehaviorTrees
{
    public sealed class BehaviorSuffleSequence : BehaviorNode, IBehaviorCompositor
    {
        public BehaviorSuffleSequence(BehaviorNode child) : base(child) { }

        public BehaviorSuffleSequence(params BehaviorNode[] children) : base(children) { }

        public BehaviorSuffleSequence(IEnumerable<BehaviorNode> children) : base(children) { }

        public override BehaviorState Evaluate(object target, float delta)
        {
            int offset = MathS.Random.Next(Children.Count);
            bool running = false;
            for (int i = 0; i < Children.Count; i++)
            {
                int offsetIndex = i + offset;
                if (offsetIndex >= Children.Count)
                    offsetIndex -= Children.Count;

                BehaviorState state = Children[offsetIndex].Evaluate(target, delta);

                if (state == BehaviorState.FAILED)
                    return BehaviorState.FAILED;
                else if (!running && state == BehaviorState.RUNNING)
                    running = true;
            }

            return running ? BehaviorState.RUNNING : BehaviorState.SUCCESS;
        }
    }
}
