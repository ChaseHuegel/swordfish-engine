namespace Swordfish.Library.BehaviorTrees;

public interface IBehaviorJob
{
    BehaviorState Tick(float delta);
}

public sealed class BehaviorJob<TTarget> : IBehaviorJob where TTarget : class
{
    private readonly BehaviorTree<TTarget> _tree;

    private readonly TTarget _target;

    public BehaviorJob(BehaviorTree<TTarget> tree, TTarget target)
    {
        _target = target;
        _tree = tree;
    }

    public BehaviorState Tick(float delta)
    {
        return _tree.Tick(_target, delta);
    }
}

public abstract class AbstractBehaviorJob<TTarget> : IBehaviorJob where TTarget : class
{
    private readonly BehaviorTree<TTarget> _tree;

    private readonly TTarget _target;

    public AbstractBehaviorJob(TTarget target)
    {
        _target = target;
        _tree = TreeFactory();
    }

    public BehaviorState Tick(float delta)
    {
        return _tree.Tick(_target, delta);
    }

    protected abstract BehaviorTree<TTarget> TreeFactory();
}