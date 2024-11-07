namespace Swordfish.Library.BehaviorTrees;

public sealed class BehaviorRunner : BehaviorNode
{
    private readonly IBehaviorJob[] _jobs;

    public BehaviorRunner(params IBehaviorJob[] jobs)
    {
        _jobs = jobs;
    }

    public override BehaviorState Evaluate(object target, float delta)
    {
        return Tick(delta);
    }

    public BehaviorState Tick(float delta)
    {
        var running = false;
        var anySuccess = false;

        for (var i = 0; i < _jobs.Length; i++)
        {
            BehaviorState state = _jobs[i].Tick(delta);

            if (!anySuccess && state == BehaviorState.SUCCESS)
            {
                anySuccess = true;
            }
            else if (!running && state == BehaviorState.RUNNING)
            {
                running = true;
            }
        }

        if (!anySuccess)
        {
            return BehaviorState.FAILED;
        }

        return running ? BehaviorState.RUNNING : BehaviorState.SUCCESS;
    }
}