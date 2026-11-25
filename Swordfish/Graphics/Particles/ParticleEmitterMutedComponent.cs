using Krill;

namespace Swordfish.Graphics.Particles;

/// <summary>Suppresses continuous particle emission on an entity without changing <see cref="ParticleEmitterComponent.Playing"/>.</summary>
public readonly struct ParticleEmitterMutedComponent : IDataComponent;
