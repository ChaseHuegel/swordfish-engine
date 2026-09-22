using Swordfish.ECS;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Networking.Components;

/// <summary>
/// The server-authoritative game mode of a player, replicated downstream. Authored by the server after
/// the join-time seed; the client reads it to decide whether interactions consume resources. The wire
/// carries an <c>int</c> (the <see cref="GameMode"/> enum underlying value); the semantic value is
/// exposed through <see cref="Mode"/> so callers never touch the raw enum externalization.
/// </summary>
[NetworkComponent(14, NetworkDirection.ServerOwned)]
public partial struct GameModeComponent : IDataComponent
{
    public GameMode Mode
    {
        readonly get => (GameMode)Value;
        set => Value = (int)value;
    }

    public GameModeComponent(GameMode gameMode) : this((int)gameMode) { }
}