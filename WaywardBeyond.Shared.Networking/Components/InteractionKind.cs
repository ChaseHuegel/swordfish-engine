namespace WaywardBeyond.Shared.Networking.Components;

/// <summary>
/// The discrete button edge of an <see cref="InteractionEvent"/>. Carried at the event's root; it is
/// orthogonal to any hint payload (which hint, if any, is set is the actual union discriminator).
/// </summary>
public enum InteractionKind : byte
{
    None,
    PrimaryPressed,
    PrimaryReleased,
    SecondaryPressed,
    SecondaryReleased,
}