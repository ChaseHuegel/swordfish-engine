namespace Swordfish.Library.BehaviorTrees;

public interface IBehaviorJob
{
    BehaviorState Tick(float delta);
}

// ReSharper disable once UnusedType.Global
public sealed class BehaviorJob<TTarget>(in BehaviorTree<TTarget> tree, in TTarget target) : IBehaviorJob
    where TTarget : class
{
    private readonly BehaviorTree<TTarget> _tree = tree;
    private readonly TTarget _target = target;

    public BehaviorState Tick(float delta)
    {
        return _tree.Tick(_target, delta);
    }
}