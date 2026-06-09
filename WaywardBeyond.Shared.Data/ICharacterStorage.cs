using System.Collections.Generic;

namespace WaywardBeyond.Shared.Data;

public interface ICharacterStorage
{
    Character? GetCharacter(string guid);
    IEnumerable<Character> GetAllCharacters();
    bool SaveCharacter(in Character character);
    bool DeleteCharacter(string guid);
}
