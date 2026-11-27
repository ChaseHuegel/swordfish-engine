using Krill;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Components;

/// <summary>Replicated parent attachment metadata for a particle emitter entity.</summary>
public partial struct ParticleEmitterBindingComponent : IDataComponent
{
    /// <summary>Parent entity UUID followed locally on clients, or <see cref="Uuid.Null"/> for world space.</summary>
    public readonly Uuid ParentUuid => Uuid.FromValue(ParentEntity);
}
