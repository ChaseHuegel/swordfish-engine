namespace Swordfish.Library.BehaviorTrees;

// ReSharper disable once UnusedType.Global
public sealed class BehaviorWait(in float seconds) : BehaviorNode, IBehaviorAction
{
    private readonly float _seconds = seconds;
    private float _elapsed;

    public override BehaviorState Evaluate(object target, float delta)
    {
        _elapsed += delta;

        if (_elapsed < _seconds)
        {
            return BehaviorState.RUNNING;
        }

        _elapsed = 0f;
        return BehaviorState.SUCCESS;

    }
}