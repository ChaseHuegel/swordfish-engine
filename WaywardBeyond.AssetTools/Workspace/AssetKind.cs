namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Content asset categories indexed and edited by Asset Tools.
/// </summary>
public enum AssetKind
{
    /// <summary>
    ///     Item definition under <c>items/</c>.
    /// </summary>
    Item = 0,

    /// <summary>
    ///     Brick definition under <c>bricks/</c>.
    /// </summary>
    Brick = 1,

    /// <summary>
    ///     Paint definition under <c>paints/</c>.
    /// </summary>
    Paint = 2,

    /// <summary>
    ///     Skill definition under <c>skills/</c>.
    /// </summary>
    Skill = 3,

    /// <summary>
    ///     Body sprite definition under <c>bodies/</c>.
    /// </summary>
    Body = 4,

    /// <summary>
    ///     Engine material definition under <c>materials/</c>.
    /// </summary>
    Material = 5,

    /// <summary>
    ///     Binary blueprint file under <c>blueprints/</c>.
    /// </summary>
    Blueprint = 6,

    /// <summary>
    ///     Localization table under <c>lang/</c>.
    /// </summary>
    Localization = 7,

    /// <summary>
    ///     Tag list under <c>lang/**/tags/</c>.
    /// </summary>
    Tag = 8
}
