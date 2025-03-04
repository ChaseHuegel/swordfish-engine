using System.Collections.Generic;
using Swordfish.Library.Util;

namespace Swordfish.Library.BehaviorTrees;

// ReSharper disable once UnusedType.Global
public sealed class BehaviorShuffleSelector : BehaviorNode, IBehaviorCompositor
{
    public BehaviorShuffleSelector(BehaviorNode child) : base(child) { }

    public BehaviorShuffleSelector(params BehaviorNode[] children) : base(children) { }

    public BehaviorShuffleSelector(IEnumerable<BehaviorNode> children) : base(children) { }

    public override BehaviorState Evaluate(object target, float delta)
    {
        int offset = MathS.Random.Next(Children.Count);
        for (var i = 0; i < Children.Count; i++)
        {
            int offsetIndex = i + offset;
            if (offsetIndex >= Children.Count)
            {
                offsetIndex -= Children.Count;
            }

            BehaviorState state = Children[offsetIndex].Evaluate(target, delta);

            if (state != BehaviorState.FAILED)
            {
                return state;
            }
        }

        return BehaviorState.FAILED;
    }
}