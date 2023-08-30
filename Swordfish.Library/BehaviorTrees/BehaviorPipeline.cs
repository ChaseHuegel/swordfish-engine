namespace Swordfish.Library.BehaviorTrees
{
    public sealed class BehaviorPipeline : BehaviorNode
    {
        private readonly IBehaviorJob[] Jobs;

        public BehaviorPipeline(params IBehaviorJob[] jobs)
        {
            Jobs = jobs;
        }

        public override BehaviorState Evaluate(object target, float delta)
        {
            return Tick(delta);
        }

        public BehaviorState Tick(float delta)
        {
            for (int i = 0; i < Jobs.Length; i++)
            {
                BehaviorState state = Jobs[i].Tick(delta);

                if (state != BehaviorState.SUCCESS)
                    return state;
            }

            return BehaviorState.SUCCESS;
        }
    }
}