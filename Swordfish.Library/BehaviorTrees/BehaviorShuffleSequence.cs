using System.Collections.Generic;
using Swordfish.Library.Util;

namespace Swordfish.Library.BehaviorTrees;

// ReSharper disable once UnusedType.Global
public sealed class BehaviorShuffleSequence : BehaviorNode, IBehaviorCompositor
{
    public BehaviorShuffleSequence(BehaviorNode child) : base(child) { }

    public BehaviorShuffleSequence(params BehaviorNode[] children) : base(children) { }

    public BehaviorShuffleSequence(IEnumerable<BehaviorNode> children) : base(children) { }

    public override BehaviorState Evaluate(object target, float delta)
    {
        int offset = MathS.Random.Next(Children.Count);
        var running = false;
        for (var i = 0; i < Children.Count; i++)
        {
            int offsetIndex = i + offset;
            if (offsetIndex >= Children.Count)
            {
                offsetIndex -= Children.Count;
            }

            BehaviorState state = Children[offsetIndex].Evaluate(target, delta);

            if (state == BehaviorState.FAILED)
            {
                return BehaviorState.FAILED;
            }

            if (!running && state == BehaviorState.RUNNING)
            {
                running = true;
            }
        }

        return running ? BehaviorState.RUNNING : BehaviorState.SUCCESS;
    }
}