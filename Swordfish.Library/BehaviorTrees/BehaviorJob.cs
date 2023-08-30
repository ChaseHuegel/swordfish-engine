namespace Swordfish.Library.BehaviorTrees
{
    public interface IBehaviorJob
    {
        BehaviorState Tick(float delta);
    }

    public sealed class BehaviorJob<TTarget> : IBehaviorJob where TTarget : class
    {
        private readonly BehaviorTree<TTarget> Tree;

        private readonly TTarget Target;

        public BehaviorJob(BehaviorTree<TTarget> tree, TTarget target)
        {
            Target = target;
            Tree = tree;
        }

        public BehaviorState Tick(float delta)
        {
            return Tree.Tick(Target, delta);
        }
    }

    public abstract class AbstractBehaviorJob<TTarget> : IBehaviorJob where TTarget : class
    {
        private readonly BehaviorTree<TTarget> Tree;

        private readonly TTarget Target;

        public AbstractBehaviorJob(TTarget target)
        {
            Target = target;
            Tree = TreeFactory();
        }

        public BehaviorState Tick(float delta)
        {
            return Tree.Tick(Target, delta);
        }

        protected abstract BehaviorTree<TTarget> TreeFactory();
    }
}