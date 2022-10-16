namespace Swordfish.Library.BehaviorTrees
{
    public class BehaviorTree<TTarget> where TTarget : class
    {
        public readonly BehaviorNode Root;

        public BehaviorTree(BehaviorNode root)
        {
            Root = root;
        }

        public BehaviorState Tick(TTarget target, float delta)
        {
            return Root.Evaluate(target, delta);
        }
    }
}
