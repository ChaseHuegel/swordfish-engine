namespace Swordfish.Library.BehaviorTrees
{
    public sealed class BehaviorWait : BehaviorNode
    {
        private readonly float Seconds;
        private float Elapsed;

        public BehaviorWait(float seconds)
        {
            Seconds = seconds;
        }

        public override BehaviorState Evaluate(object target, float delta)
        {
            Elapsed += delta;

            if (Elapsed >= Seconds)
            {
                Elapsed = 0f;
                return BehaviorState.SUCCESS;
            }

            return BehaviorState.RUNNING;
        }
    }
}
