namespace Swordfish.Library.BehaviorTrees;

public sealed class BehaviorTree<TTarget>(in BehaviorNode root) : BehaviorNode<TTarget>
    where TTarget : class
{
    public readonly BehaviorNode Root = root;

    public override BehaviorState Evaluate(TTarget target, float delta)
    {
        return Tick(target, delta);
    }

    public BehaviorState Tick(TTarget target, float delta)
    {
        return Root.Evaluate(target, delta);
    }
}