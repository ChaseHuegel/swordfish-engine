using Swordfish.ECS;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Components;

public struct CharacterComponent(in Character character) : IDataComponent
{
    public readonly Character Character = character;
}