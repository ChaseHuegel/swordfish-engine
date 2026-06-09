using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Data;

public interface ICharacterStorage
{
    Result<Character> GetCharacter(string guid);
    
    IEnumerable<Character> GetAllCharacters();
    
    Result SaveCharacter(in Character character);
    
    Result DeleteCharacter(string guid);
}
