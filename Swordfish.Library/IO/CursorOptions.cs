using System;

namespace Swordfish.Library.IO;

[Flags]
public enum CursorOptions
{
    None = 0,
    Hidden = 1,
    Locked = 2,
    Confined = 4,
}