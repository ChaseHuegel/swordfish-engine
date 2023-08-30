namespace Swordfish.Library.BehaviorTrees
{
    public sealed class BehaviorRunner : BehaviorNode
    {
        private readonly IBehaviorJob[] Jobs;

        public BehaviorRunner(params IBehaviorJob[] jobs)
        {
            Jobs = jobs;
        }

        public override BehaviorState Evaluate(object target, float delta)
        {
            return Tick(delta);
        }

        public BehaviorState Tick(float delta)
        {
            bool running = false;
            bool anySuccess = false;

            for (int i = 0; i < Jobs.Length; i++)
            {
                BehaviorState state = Jobs[i].Tick(delta);

                if (!anySuccess && state == BehaviorState.SUCCESS)
                    anySuccess = true;
                else if (!running && state == BehaviorState.RUNNING)
                    running = true;
            }

            if (!anySuccess)
                return BehaviorState.FAILED;

            return running ? BehaviorState.RUNNING : BehaviorState.SUCCESS;
        }
    }
}