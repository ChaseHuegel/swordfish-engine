using System.Numerics;
using System.Runtime.InteropServices;

namespace Swordfish.Graphics.Jolt;

/// <summary>
///     Packed 16-byte vertex for Jolt physics debug geometry.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct JoltDebugVertex(in Vector3 position, uint packedColor)
{
    public readonly Vector3 Position = position;
    public readonly uint PackedColor = packedColor;
}
