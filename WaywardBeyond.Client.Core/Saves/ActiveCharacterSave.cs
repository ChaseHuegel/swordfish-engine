using System.Threading;

namespace WaywardBeyond.Client.Core.Saves;

internal class ActiveCharacterSave
{
    public CharacterSave? ActiveSave
    {
        get
        {
            using Lock.Scope _ = _activeSaveLock.EnterScope();
            return _activeSave;
        }
        set
        {
            using Lock.Scope _ = _activeSaveLock.EnterScope();
            _activeSave = value;
        }
    }
    
    private readonly Lock _activeSaveLock = new();
    private CharacterSave? _activeSave;
}