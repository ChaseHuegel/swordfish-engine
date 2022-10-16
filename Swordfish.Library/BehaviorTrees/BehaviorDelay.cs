using System;

namespace Swordfish.Library.BehaviorTrees
{
    public sealed class BehaviorDelay : BehaviorNode
    {
        private readonly float Delay;
        private float Elapsed;

        public BehaviorDelay(float delay, BehaviorNode child) : base(child)
        {
            Delay = delay;
        }

        public override BehaviorState Evaluate(object target, float delta)
        {
            Elapsed += delta;

            if (Elapsed >= Delay)
            {
                Elapsed = 0f;
                Children[0].Evaluate(target, delta);
                return BehaviorState.SUCCESS;
            }

            return BehaviorState.RUNNING;
        }
    }
}
