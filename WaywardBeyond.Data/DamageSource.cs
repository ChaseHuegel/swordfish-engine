namespace WaywardBeyond.Data;

/// <summary>The origin of one health-damaging event.</summary>
public enum DamageSource : byte
{
    /// <summary>No damage source was recorded.</summary>
    None = 0,

    /// <summary>Damage from breathing vacuum without oxygen.</summary>
    Suffocation = 1,

    /// <summary>Damage from a hard collision with a physics body.</summary>
    Kinetic = 2,
}
