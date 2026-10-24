namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>What one handler decides about the interaction it just processed.</summary>
public enum InteractionDecision : byte
{
    /// <summary>Pass the running context to the next handler in the same stage.</summary>
    Continue = 0,

    /// <summary>
    /// Skip the remaining handlers in this stage and continue to the next stage. In
    /// <see cref="InteractionStage.Apply"/> this asserts the outcome is now authoritative.
    /// </summary>
    Handled = 1,

    /// <summary>Own the whole interaction; no later stage runs.</summary>
    Consume = 2,

    /// <summary>Reject the interaction; no later stage runs and no outcome is recorded.</summary>
    Cancel = 3,
}
