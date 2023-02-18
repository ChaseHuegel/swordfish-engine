using System.Diagnostics;
using Swordfish.Library.BehaviorTrees;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests
{
    public class BehaviorTreeTests : TestBase
    {
        private readonly BehaviorTree<TestTarget> Tree;

        public BehaviorTreeTests(ITestOutputHelper output) : base(output)
        {
            Tree = new BehaviorTree<TestTarget>(
                new BehaviorSelector(
                    new BehaviorSequence(
                        new TargetIsNull(),
                        new DummyAction()
                    ),
                    new IfTargetIsTest(
                        new DummyAction()
                    ),
                    new IfTargetIsTest(
                        new DummyAction()
                    ),
                    new IfTargetIsTest(
                        new DummyAction()
                    ),
                    new IfTargetIsTest(
                        new DummyAction()
                    ),
                    new IfTargetIsTest(
                        new DummyAction()
                    ),
                    new IfTargetIsDelay(
                        new BehaviorDelay(1f,
                            new DummyAction()
                        )
                    )
                )
            );
        }

        [Fact]
        public void BenchmarkEntryTick()
        {
            TestTarget target = null;

            Stopwatch sw = Stopwatch.StartNew();
            //  1k behaviors being ticked 60x/sec assuming 60fps
            for (int i = 0; i < 60 * 1000; i++)
                Tree.Tick(target, 0f);
            Output.WriteLine($"Elapsed: {sw.Elapsed.TotalMilliseconds} ms");
        }

        [Fact]
        public void BenchmarkBranchedTick()
        {
            var target = new TestTarget(TestTarget.State.DELAY);

            Stopwatch sw = Stopwatch.StartNew();
            //  1k behaviors being ticked 60x/sec assuming 60fps
            for (int i = 0; i < 60 * 1000; i++)
                Tree.Tick(target, 0f);
            Output.WriteLine($"Elapsed: {sw.Elapsed.TotalMilliseconds} ms");
        }

        [Fact]
        public void BehaviorGateSuccess() => Assert.Equal(BehaviorState.SUCCESS, Tree.Tick(new TestTarget(TestTarget.State.TEST), 0f));

        [Fact]
        public void BehaviorSequencedConditionSuccess() => Assert.Equal(BehaviorState.SUCCESS, Tree.Tick(null, 0f));

        [Fact]
        public void BehaviorTreeTickFailed() => Assert.Equal(BehaviorState.FAILED, Tree.Tick(new TestTarget(), 0f));

        private class TestTarget
        {
            public enum State
            {
                NONE,
                TEST,
                DELAY
            }

            public State CurrentState;

            public TestTarget() { }

            public TestTarget(State state)
            {
                CurrentState = state;
            }
        }

        private class TargetIsNull : BehaviorCondition<TestTarget>
        {
            public override bool Check(TestTarget target, float delta)
            {
                return target == null;
            }
        }

        private class IfTargetIsTest : BehaviorGate<TestTarget>
        {
            public IfTargetIsTest(BehaviorNode child) : base(child) { }

            public override bool Check(TestTarget target, float delta)
            {
                return target.CurrentState == TestTarget.State.TEST;
            }
        }

        private class IfTargetIsDelay : BehaviorGate<TestTarget>
        {
            public IfTargetIsDelay(BehaviorNode child) : base(child) { }

            public override bool Check(TestTarget target, float delta)
            {
                return target.CurrentState == TestTarget.State.DELAY;
            }
        }

        private class DummyAction : BehaviorNode<TestTarget>
        {
            public override BehaviorState Evaluate(TestTarget target, float delta)
            {
                return BehaviorState.SUCCESS;
            }
        }
    }
}
