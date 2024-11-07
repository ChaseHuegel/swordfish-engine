using System;

namespace Swordfish.Library.IO;

[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4,
}