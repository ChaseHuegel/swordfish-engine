using DryIoc;
using Swordfish.Library.Serialization.Toml.Mappers;
using Swordfish.Library.Types;

namespace Shoal.Extensions.Swordfish;

public static class ContainerExtensions
{
    /// <summary>
    ///     Registers a known <see cref="DataBinding{T}"/> in the container to
    ///     ensure common requirements, such as TOML de/serialization, are available.
    /// </summary>
    public static void RegisterDataBinding<T>(this IContainer container)
    {
        container.RegisterTomlMapper<DataBindingTomlMapper<T>>();
    }
}