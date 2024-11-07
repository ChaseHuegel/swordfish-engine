namespace Swordfish.Library.BehaviorTrees;

public sealed class BehaviorWait : BehaviorNode, IBehaviorAction
{
    private readonly float _seconds;
    private float _elapsed;

    public BehaviorWait(float seconds)
    {
        _seconds = seconds;
    }

    public override BehaviorState Evaluate(object target, float delta)
    {
        _elapsed += delta;

        if (_elapsed >= _seconds)
        {
            _elapsed = 0f;
            return BehaviorState.SUCCESS;
        }

        return BehaviorState.RUNNING;
    }
}