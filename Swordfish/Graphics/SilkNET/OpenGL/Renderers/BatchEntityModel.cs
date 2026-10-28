using System.Numerics;
using Swordfish.ECS;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

internal readonly struct BatchEntityModel(in Uuid entity, in Matrix4x4 matrix, in MeshBatchRenderer renderer)
{
    public readonly Uuid Entity = entity;
    public readonly Matrix4x4 Matrix = matrix;
    public readonly MeshBatchRenderer Renderer = renderer;
}
