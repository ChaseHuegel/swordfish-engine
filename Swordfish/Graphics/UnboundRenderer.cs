using Swordfish.ECS;

namespace Swordfish.Graphics;

/// <summary>
/// An unbound mesh renderer associated with an entity.
/// </summary>
internal readonly record struct UnboundRenderer(Uuid Entity, MeshRenderer Renderer);
