using Swordfish.ECS;

namespace Swordfish.Graphics;

/// <summary>
/// An unbound mesh batch renderer associated with an entity.
/// </summary>
internal readonly record struct UnboundBatchRenderer(Uuid Entity, MeshBatchRenderer Renderer);
