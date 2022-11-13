using System;

namespace Swordfish.Library.IO
{
    [Flags]
    public enum ShortcutModifiers
    {
        NONE = 0,
        CONTROL = 1,
        SHIFT = 2,
        ALT = 4
    }
}
