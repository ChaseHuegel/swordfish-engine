using System.Numerics;
using Swordfish.ECS;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

internal readonly struct EntityModel(in Uuid entity, in Matrix4x4 matrix, in MeshRenderer meshRenderer, in BoundingBox worldBounds)
{
    public readonly Uuid Entity = entity;
    public readonly Matrix4x4 Matrix = matrix;
    public readonly MeshRenderer MeshRenderer = meshRenderer;
    public readonly BoundingBox WorldBounds = worldBounds;
}
