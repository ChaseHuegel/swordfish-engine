namespace WaywardBeyond.AssetStudio.Models;

/// <summary>Selects whether the visual item editor targets first-person ViewModel or third-person WorldModel.</summary>
public enum ItemModelEditorMode
{
    /// <summary>First-person camera at <c>(0, 0, 0)</c> editing <c>[Items.ViewModel]</c>.</summary>
    ViewModel,

    /// <summary>Orbit camera with 1 m floor plane and 1.8 m body scale reference editing <c>[Items.WorldModel]</c>.</summary>
    WorldModel,
}
