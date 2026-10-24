using Swordfish.ECS;

namespace WaywardBeyond.Data.Components;

/// <summary>The game mode of an entity</summary>
public partial struct GameModeComponent : IDataComponent
{
    public GameMode Mode
    {
        readonly get => (GameMode)Value;
        set => Value = (int)value;
    }

    public GameModeComponent(GameMode gameMode) : this((int)gameMode) { }
}