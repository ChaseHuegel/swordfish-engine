using System;

namespace Reef.UI;

[Flags]
public enum Anchors
{
    Top = 1,
    Left = 2,
    Bottom = 4,
    Right = 8,
    Center = 16,
    Local = 32,
}