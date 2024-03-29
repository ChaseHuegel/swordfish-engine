using System;
using System.Collections.Generic;

namespace Swordfish.Library.BehaviorTrees
{
    public abstract class BehaviorNode
    {
        public readonly List<BehaviorNode> Children;

        public BehaviorNode(params BehaviorNode[] children)
        {
            Children = new List<BehaviorNode>(children);
        }

        public BehaviorNode(IEnumerable<BehaviorNode> children)
        {
            Children = new List<BehaviorNode>(children);
        }

        public abstract BehaviorState Evaluate(object target, float delta);
    }

    public abstract class BehaviorNode<TTarget> : BehaviorNode where TTarget : class
    {
        public BehaviorNode(params BehaviorNode[] children) : base(children) { }

        public BehaviorNode(IEnumerable<BehaviorNode> children) : base(children) { }

        public override BehaviorState Evaluate(object target, float delta) => Evaluate((TTarget)target, delta);

        public abstract BehaviorState Evaluate(TTarget target, float delta);
    }

    public class BehaviorDynamic<TTarget> : BehaviorNode<TTarget>, IBehaviorAction
        where TTarget : class
    {
        private readonly Func<TTarget, float, BehaviorState> Func;

        public BehaviorDynamic(Func<TTarget, float, BehaviorState> func)
        {
            Func = func;
        }

        public override BehaviorState Evaluate(TTarget target, float delta)
        {
            return Func(target, delta);
        }
    }
}
