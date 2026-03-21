using Swordfish.Library.IO;

namespace WaywardBeyond.Client.Core.Saves;

internal readonly struct CharacterSave(in PathInfo path, in Character character)
{
    public readonly PathInfo Path = path;
    public readonly Character Character = character;
}