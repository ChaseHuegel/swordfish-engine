using System.Numerics;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

internal readonly struct RenderInstance(in int entity, in Matrix4x4 matrix)
{
    public readonly int Entity = entity;
    public readonly Matrix4x4 Matrix = matrix;
}