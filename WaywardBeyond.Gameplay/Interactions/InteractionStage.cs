namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>The ordered stages an interaction passes through. Handlers register against one stage.</summary>
public enum InteractionStage : byte
{
    /// <summary>Determines the content the interaction operates with, such as the brick a held item places.</summary>
    Select = 0,

    /// <summary>Derives the base outcome from the client's target hint.</summary>
    Resolve = 1,

    /// <summary>Adjusts or rejects the outcome before any authority change.</summary>
    Validate = 2,

    /// <summary>Performs the authority change. <see cref="InteractionDecision.Handled"/> asserts it is authoritative.</summary>
    Commit = 3,

    /// <summary>Applies the consequences of an authoritative outcome, such as loot, consumption, and progression.</summary>
    React = 4,
}
