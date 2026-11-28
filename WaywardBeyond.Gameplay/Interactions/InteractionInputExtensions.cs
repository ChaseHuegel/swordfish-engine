using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Maps between logical <see cref="InteractionInput"/> channels, wire <see cref="InteractionKind"/> edges, and <see cref="InputComponent"/> held state.</summary>
public static class InteractionInputExtensions
{
    /// <summary>Returns the logical <see cref="InteractionInput"/> channel corresponding to <paramref name="kind"/>.</summary>
    public static InteractionInput ToInput(this InteractionKind kind)
    {
        return kind switch
        {
            InteractionKind.PrimaryPressed or InteractionKind.PrimaryReleased => InteractionInput.Primary,
            InteractionKind.SecondaryPressed or InteractionKind.SecondaryReleased => InteractionInput.Secondary,
            InteractionKind.Activate => InteractionInput.Activate,
            _ => InteractionInput.None,
        };
    }

    /// <summary>Returns whether <paramref name="kind"/> is a release edge.</summary>
    public static bool IsRelease(this InteractionKind kind)
    {
        return kind is InteractionKind.PrimaryReleased or InteractionKind.SecondaryReleased;
    }

    /// <summary>Returns the pressed wire edge for <paramref name="input"/>, or <see cref="InteractionKind.None"/>.</summary>
    public static InteractionKind ToPressedKind(this InteractionInput input)
    {
        return input switch
        {
            InteractionInput.Primary => InteractionKind.PrimaryPressed,
            InteractionInput.Secondary => InteractionKind.SecondaryPressed,
            InteractionInput.Activate => InteractionKind.Activate,
            _ => InteractionKind.None,
        };
    }

    /// <summary>Returns the released wire edge for <paramref name="input"/>, or <see cref="InteractionKind.None"/>.</summary>
    public static InteractionKind ToReleasedKind(this InteractionInput input)
    {
        return input switch
        {
            InteractionInput.Primary => InteractionKind.PrimaryReleased,
            InteractionInput.Secondary => InteractionKind.SecondaryReleased,
            _ => InteractionKind.None,
        };
    }

    /// <summary>
    /// Returns whether <paramref name="input"/> is satisfied by <paramref name="command"/>.
    /// <see cref="InteractionInput.None"/> requires no held input; unrecognized values fail closed.
    /// </summary>
    public static bool IsHeld(this InteractionInput input, in InputComponent command)
    {
        return input switch
        {
            InteractionInput.None => true,
            InteractionInput.Primary => command.PrimaryHeld,
            InteractionInput.Secondary => command.SecondaryHeld,
            _ => false,
        };
    }
}
