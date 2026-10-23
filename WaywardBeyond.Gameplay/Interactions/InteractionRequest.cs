using System.Numerics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>The shared input the interaction resolver decides over.</summary>
public readonly struct InteractionRequest(Vector3 origin, BrickInteraction? hint, InteractionKind kind, Brick? placeable, GameMode gameMode, float reach)
{
    /// <summary>World-space interaction origin (the player's position); reach is measured from here.</summary>
    public readonly Vector3 Origin = origin;

    /// <summary>The client's resolved target voxel hint.</summary>
    public readonly BrickInteraction? Hint = hint;

    /// <summary>The input edge that produced this interaction.</summary>
    public readonly InteractionKind Kind = kind;

    /// <summary>The resolved placeable brick for the held item, when it places one.</summary>
    public readonly Brick? Placeable = placeable;

    /// <summary>The game mode this interaction was made in.</summary>
    public readonly GameMode GameMode = gameMode;

    /// <summary>Maximum distance the interaction may reach, from the origin.</summary>
    public readonly float Reach = reach;
}