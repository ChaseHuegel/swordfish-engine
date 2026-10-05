using Swordfish.ECS;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Marks a server world system: resolved per world by the world host and ticked in registration order by
/// the engine <see cref="World"/>. Server systems are registered under this marker service rather than
/// <see cref="IEntitySystem"/> directly, so the client's ECS context can never resolve them and vice
/// versa. Third-party modules add server systems with
/// <see cref="ServerComposition.RegisterServerSystem{T}"/>, which places them after the built-ins.
/// </summary>
public interface IServerWorldSystem : IEntitySystem
{
}