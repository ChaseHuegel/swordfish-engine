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
    ///     which will be loaded from VFS or install `config/`, or else initialized from defaults.
    /// </summary>
    public static void RegisterConfig<T>(this IContainer container, string file, SaveLocation saveLocation = SaveLocation.UserData)
        where T : Config<T>, new()
    {
        container.RegisterTomlParser<T>();
        container.RegisterDelegate<T>(
            context => LoadOrCreateConfig<T>(context, file, saveLocation),
            Reuse.Singleton,
            ifAlreadyRegistered: IfAlreadyRegistered.Keep);
    }

    private static T LoadOrCreateConfig<T>(IResolverContext context, string file, SaveLocation saveLocation)
        where T : Config<T>, new()
    {
        var configPath = new PathInfo("config/");
        PathInfo defaultPath = configPath.At(file);

        var vfs = context.Resolve<VirtualFileSystem>();
        if (!vfs.TryGetFile(defaultPath, out PathInfo readPath))
        {
            PathInfo[] files = vfs.GetFiles(configPath, SearchOption.AllDirectories);
            PathInfo? nestedMatch = files.Cast<PathInfo?>().FirstOrDefault(pathInfo => pathInfo!.Value.GetFileName() == file);
            readPath = nestedMatch ?? defaultPath;
        }

        var fileParseService = context.Resolve<IFileParseService>();
        if (!fileParseService.TryParse<T>(readPath, out T result))
        {
            result = new T();
        }

        result.Path = saveLocation switch
        {
            SaveLocation.Install => defaultPath,
            _ => context.Resolve<AppPaths>().ConfigRoot.At(file),
        };
        return result;
    }
}