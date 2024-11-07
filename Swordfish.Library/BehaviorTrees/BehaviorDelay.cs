namespace Swordfish.Library.BehaviorTrees;

public sealed class BehaviorDelay(in float delay, in BehaviorNode child) : BehaviorNode(child), IBehaviorDecorator
{
    private readonly float _delay = delay;
    private float _elapsed;

    public override BehaviorState Evaluate(object target, float delta)
    {
        _elapsed += delta;

        if (_elapsed < _delay)
        {
            return BehaviorState.RUNNING;
        }

        _elapsed = 0f;
        return Children[0].Evaluate(target, delta);

    }
}