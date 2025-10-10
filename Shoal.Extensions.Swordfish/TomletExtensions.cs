using DryIoc;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;

namespace Shoal.Extensions.Swordfish;

public static class TomletExtensions
{
    /// <summary>
    ///     Registers a TOML <see cref="IFileParser"/> for the provided <typeparamref name="T"/>.
    /// </summary>
    public static void RegisterTomlParser<T>(this IContainer container)
    {
        container.RegisterMany<TomlParser<T>>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
    }
    
    /// <summary>
    ///     Registers a TOML de/serialization mapper.
    /// </summary>
    public static void RegisterTomlMapper<T>(this IContainer container) where T : ITomlMapper
    {
        container.RegisterMany<T>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
    }
}
