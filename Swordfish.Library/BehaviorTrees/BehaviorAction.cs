namespace Swordfish.Library.BehaviorTrees
{
    public abstract class BehaviorAction<TTarget> : BehaviorNode<TTarget>, IBehaviorAction
    where TTarget : class
    {
        public override BehaviorState Evaluate(TTarget target, float delta)
        {
            Run(target, delta);
            return BehaviorState.SUCCESS;
        }

        public abstract void Run(TTarget target, float delta);
    }
}
