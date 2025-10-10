using DryIoc;
using Swordfish.Library.Configuration;
using Swordfish.Library.IO;
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
    
    /// <summary>
    ///     Registers a config file modeled by type <typeparamref name="T"/>,
    ///     which will be loaded from `assets/config/` or else initialized from defaults.
    /// </summary>
    public static void RegisterConfig<T>(this IContainer container, string file) 
        where T : Config<T>, new()
    {
        container.RegisterTomlParser<T>();
        container.RegisterDelegate<T>(context => LoadOrCreateConfig<T>(context, file), Reuse.Singleton);
    }
    
    private static T LoadOrCreateConfig<T>(IResolverContext context, string file) 
        where T : Config<T>, new()
    {
        PathInfo path = new PathInfo("assets/config/").At(file);
        
        var fileParseService = context.Resolve<IFileParseService>();
        if (!fileParseService.TryParse<T>(path, out T result))
        {
            result = new T();
        }

        result.Path = path;
        return result;
    }
}