using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Data;

public interface ICharacterStorage
{
    Result<Character> GetCharacter(ulong id);
    
    IEnumerable<Character> GetAllCharacters();
    
    Result SaveCharacter(Character character);
    
    Result DeleteCharacter(ulong id);
}
