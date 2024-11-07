namespace Swordfish.Library.BehaviorTrees;

// ReSharper disable once UnusedType.Global
public sealed class BehaviorPipeline : BehaviorNode
{
    private readonly IBehaviorJob[] _jobs;

    public BehaviorPipeline(params IBehaviorJob[] jobs)
    {
        _jobs = jobs;
    }

    public override BehaviorState Evaluate(object target, float delta)
    {
        return Tick(delta);
    }

    public BehaviorState Tick(float delta)
    {
        for (var i = 0; i < _jobs.Length; i++)
        {
            BehaviorState state = _jobs[i].Tick(delta);

            if (state != BehaviorState.SUCCESS)
            {
                return state;
            }
        }

        return BehaviorState.SUCCESS;
    }
}