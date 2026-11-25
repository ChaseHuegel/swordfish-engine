namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     File-backed resource categories referenced by content definitions.
/// </summary>
public enum ResourceKind
{
    /// <summary>
    ///     Image file under <c>textures/</c> addressed with extension.
    /// </summary>
    Texture = 0,

    /// <summary>
    ///     OBJ mesh file under <c>meshes/</c> addressed with extension.
    /// </summary>
    Mesh = 1,

    /// <summary>
    ///     Audio clip file under <c>audio/</c> addressed with extension.
    /// </summary>
    Audio = 2,

    /// <summary>
    ///     Shader file under <c>shaders/</c> addressed without extension.
    /// </summary>
    Shader = 3
}
