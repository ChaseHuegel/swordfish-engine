namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
/// Declares whether an interaction requires a sustained timed hold before entering the
/// <see cref="InteractionStage.Commit"/> and <see cref="InteractionStage.React"/> stages.
/// </summary>
public readonly struct TimedInteractionSpec(
    bool isRequired,
    TimedInteractionKey key,
    float requiredDuration,
    TimedInteractionInput requiredInput,
    bool cancelOnEquipmentChange
) {
    /// <summary>An immediate interaction that commits without a timed hold.</summary>
    public static TimedInteractionSpec None => default;

    /// <summary>Whether the interaction must complete a timed hold before committing.</summary>
    public readonly bool IsRequired = isRequired;

    /// <summary>The target identity used to detect retargeting or target mutation across ticks.</summary>
    public readonly TimedInteractionKey Key = key;

    /// <summary>Total hold duration in seconds required for completion.</summary>
    public readonly float RequiredDuration = float.IsFinite(requiredDuration) && requiredDuration >= 0f ? requiredDuration : 0f;

    /// <summary>The continuous input condition that must remain held for the session to advance.</summary>
    public readonly TimedInteractionInput RequiredInput = requiredInput;

    /// <summary>Whether changing active hotbar slot or held item cancels the active hold.</summary>
    public readonly bool CancelOnEquipmentChange = cancelOnEquipmentChange;

    /// <summary>Creates a timed hold requirement for an interaction.</summary>
    public static TimedInteractionSpec Require(
        TimedInteractionKey key,
        float requiredDuration,
        TimedInteractionInput requiredInput = TimedInteractionInput.Primary,
        bool cancelOnEquipmentChange = true
    ) {
        return new TimedInteractionSpec(
            isRequired: true,
            key,
            requiredDuration,
            requiredInput,
            cancelOnEquipmentChange);
    }
}
