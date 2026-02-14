using Swordfish.Library.IO;

namespace WaywardBeyond.Client.Core.Shortcuts;

public abstract class ShortcutRegistration
{
    protected abstract string Name { get; }
    protected abstract string Category { get; }
    protected virtual string? Description { get; }
    protected abstract ShortcutModifiers Modifiers { get; }
    protected abstract Key Key { get; }

    protected abstract bool IsEnabled();
    protected abstract void Action();
    protected virtual void Released() { }
    
    public Shortcut Create()
    {
        return new Shortcut
        {
            Name = Name,
            Category = Category,
            Description = Description,
            Modifiers = Modifiers,
            Key = Key,
            IsEnabled = IsEnabled,
            Action = Action,
            Released = Released,
        };
    }
}