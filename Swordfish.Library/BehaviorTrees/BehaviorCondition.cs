namespace Swordfish.Library.BehaviorTrees
{
    public abstract class BehaviorCondition<TTarget> : BehaviorNode<TTarget> where TTarget : class
    {
        public override BehaviorState Evaluate(TTarget target, float delta)
        {
            return Check(target, delta) ? BehaviorState.SUCCESS : BehaviorState.FAILED;
        }

        public abstract bool Check(TTarget target, float delta);
    }
}
