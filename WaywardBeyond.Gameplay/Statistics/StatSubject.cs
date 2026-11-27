namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>Declares which primary subject supplies the base value for a <see cref="StatQuery"/>.</summary>
public enum StatSubject : byte
{
    /// <summary>Any matching base modifier source may contribute.</summary>
    Any = 0,

    /// <summary>The targeted brick supplies the base value.</summary>
    Brick = 1,

    /// <summary>The held item supplies the base value.</summary>
    Item = 2,

    /// <summary>The acting entity supplies the base value.</summary>
    Entity = 3,
}
