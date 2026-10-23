using Swordfish.ECS;
using WaywardBeyond.Data.Registry;

namespace WaywardBeyond.Data.Components;

/// <summary>The game mode of an entity</summary>
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