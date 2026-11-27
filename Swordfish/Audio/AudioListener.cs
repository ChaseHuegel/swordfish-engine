using Krill;
using Swordfish.ECS;

namespace Swordfish.Audio;

/// <summary>Marks an entity with a <see cref="TransformComponent"/> as the active 3D audio listener.</summary>
public readonly struct AudioListener : IDataComponent;
