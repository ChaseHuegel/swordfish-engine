namespace Swordfish.Library.BehaviorTrees;

public sealed class BehaviorDelay : BehaviorNode, IBehaviorDecorator
{
    private readonly float _delay;
    private float _elapsed;

    public BehaviorDelay(float delay, BehaviorNode child) : base(child)
    {
        _delay = delay;
    }

    public override BehaviorState Evaluate(object target, float delta)
    {
        _elapsed += delta;

        if (_elapsed >= _delay)
        {
            _elapsed = 0f;
            return Children[0].Evaluate(target, delta);
        }

        return BehaviorState.RUNNING;
    }
}