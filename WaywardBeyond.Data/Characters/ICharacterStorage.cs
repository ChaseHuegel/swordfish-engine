using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Characters;

/// <summary>Provides access to character data.</summary>
public interface ICharacterStorage
{
    /// <summary>Attempts to get data for the provided character.</summary>
    Result<Character> GetCharacter(ulong id);
    
    /// <summary>Gets data for all characters.</summary>
    IReadOnlyList<Character> GetAllCharacters();
    
    /// <summary>Attempts to save data for the provided character.</summary>
    Result SaveCharacter(Character character);
    
    /// <summary>Attempts to delete data for the provided character.</summary>
    Result DeleteCharacter(ulong id);
}
