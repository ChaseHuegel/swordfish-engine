namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>The execution order of handlers inside one stage. Ties keep registration order.</summary>
public enum InteractionPriority : byte
{
    /// <summary>Runs before normal handlers; used to pre-empt the built-ins.</summary>
    High = 0,

    /// <summary>The default band for gameplay and content handlers.</summary>
    Normal = 1,

    /// <summary>Runs after normal handlers; the built-in authority write registers here.</summary>
    Low = 2,
}
