namespace Swordfish.Library.BehaviorTrees;

public abstract class BehaviorGate<TTarget> : BehaviorNode<TTarget>, IBehaviorDecorator
    where TTarget : class
{
    public BehaviorGate(BehaviorNode child) : base(child) { }

    public override BehaviorState Evaluate(TTarget target, float delta)
    {
        if (Check(target, delta))
        {
            return Children[0].Evaluate(target, delta);
        }
        else
        {
            return BehaviorState.FAILED;
        }
    }

    public abstract bool Check(TTarget target, float delta);
}