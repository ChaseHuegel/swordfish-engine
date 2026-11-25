namespace WaywardBeyond.AssetStudio.Models;

/// <summary>Framing aspect presets available in first-person ViewModel editing mode.</summary>
public enum ItemModelAspectPreset
{
    /// <summary>Standard widescreen 16:9 letterbox frame.</summary>
    Ratio16x9,

    /// <summary>Widescreen 16:10 letterbox frame.</summary>
    Ratio16x10,

    /// <summary>Classic 4:3 letterbox frame.</summary>
    Ratio4x3,

    /// <summary>Unconstrained viewport fill matching window proportions.</summary>
    Fill,
}
