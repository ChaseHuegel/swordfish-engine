using System.Numerics;
using Reef.UI;

namespace WaywardBeyond.Client.UI.Theme;

/// <summary>Resolved colors and border treatment for one control state.</summary>
internal readonly record struct ControlStyle(
    Vector4 Fill,
    Vector4 Border,
    int BorderThickness,
    CornerCapStyle Caps,
    Vector4 Text
);
