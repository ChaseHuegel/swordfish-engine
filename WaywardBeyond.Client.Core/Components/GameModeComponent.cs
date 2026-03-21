using Swordfish.ECS;
using WaywardBeyond.Client.Core.Characters;

namespace WaywardBeyond.Client.Core.Components;

public struct GameModeComponent(in GameMode gameMode) : IDataComponent
{
    public readonly GameMode GameMode = gameMode;
}