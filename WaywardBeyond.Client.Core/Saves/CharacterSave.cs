namespace WaywardBeyond.Client.Core.Saves;

internal readonly struct CharacterSave(in Character character)
{
    public readonly Character Character = character;
}
