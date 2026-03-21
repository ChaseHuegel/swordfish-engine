using Swordfish.ECS;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.Components;

public struct CharacterComponent(in Character character) : IDataComponent
{
    public readonly Character Character = character;
}