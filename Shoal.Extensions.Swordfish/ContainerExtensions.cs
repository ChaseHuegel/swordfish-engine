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
    ///     which will be loaded from `config/` or else initialized from defaults.
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
        var configPath = new PathInfo("config/");
        
        var vfs = context.Resolve<VirtualFileSystem>();
        PathInfo[] files = vfs.GetFiles(configPath, SearchOption.AllDirectories);
        
        //  Attempt to resolve the config from VFS else resolve it at the root
        PathInfo path = files.Length > 0 ? files[0] : configPath.At(file);
        
        var fileParseService = context.Resolve<IFileParseService>();
        if (!fileParseService.TryParse<T>(path, out T result))
        {
            result = new T();
        }

        result.Path = path;
        return result;
    }
}