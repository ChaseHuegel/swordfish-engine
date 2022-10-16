using System;

namespace Swordfish.Library.BehaviorTrees
{
    public sealed class BehaviorInverter : BehaviorNode
    {
        public BehaviorInverter(BehaviorNode child) : base(child) { }

        public override BehaviorState Evaluate(object target, float delta)
        {
            switch (Children[0].Evaluate(target, delta))
            {
                case BehaviorState.RUNNING:
                    return BehaviorState.RUNNING;
                case BehaviorState.SUCCESS:
                    return BehaviorState.FAILED;
                case BehaviorState.FAILED:
                    return BehaviorState.SUCCESS;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
