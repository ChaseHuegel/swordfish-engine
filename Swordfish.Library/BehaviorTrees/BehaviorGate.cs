namespace Swordfish.Library.BehaviorTrees;

public abstract class BehaviorGate<TTarget>(in BehaviorNode child) : BehaviorNode<TTarget>(child), IBehaviorDecorator
    where TTarget : class
{
    public override BehaviorState Evaluate(TTarget target, float delta)
    {
        return Check(target, delta) ? Children[0].Evaluate(target, delta) : BehaviorState.FAILED;
    }

    public abstract bool Check(TTarget target, float delta);
}