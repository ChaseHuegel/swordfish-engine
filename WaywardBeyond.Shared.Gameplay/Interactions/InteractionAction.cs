namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// The resolved outcome of a player interaction. <see cref="None"/> is the first-class outcome of a
/// hint-less event (or one whose hint fails validation) - never an error.
/// </summary>
public enum InteractionAction : byte
{
    None = 0,
    Break = 1,
    Place = 2,
}