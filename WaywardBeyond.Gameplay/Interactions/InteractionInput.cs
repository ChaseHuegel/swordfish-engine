namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Logical player interaction input channel shared by client and server interaction flows.</summary>
public enum InteractionInput : byte
{
    /// <summary>No interaction input channel.</summary>
    None = 0,

    /// <summary>The primary interaction input channel.</summary>
    Primary = 1,

    /// <summary>The secondary interaction input channel.</summary>
    Secondary = 2,
}
