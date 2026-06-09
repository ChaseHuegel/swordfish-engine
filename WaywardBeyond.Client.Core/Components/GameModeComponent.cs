using Swordfish.ECS;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Components;

public struct GameModeComponent(in GameMode gameMode) : IDataComponent
{
    public readonly GameMode GameMode = gameMode;
}