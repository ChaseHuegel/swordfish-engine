using Karambolo.Extensions.Logging.File;
using Microsoft.Extensions.Logging.Console;
using Shoal.Globalization;
using Shoal.Modularity;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Events;
using Swordfish.Library.Serialization;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Serialization.Toml.Mappers;

namespace Shoal;

public sealed class AppEngine : IDisposable
{
    private static readonly LogListener _logListener = new();
    private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(BuildLoggerFactory);
    private static readonly ILogger _logger = CreateLogger<AppEngine>();

    public IContainer Container { get; }
    
    private static readonly SwitchDictionary<string?, Assembly, IModulePathService> _modulePathServices = new();

    private AppEngine(in string[] args, in TextWriter output)
    {
        IContainer coreContainer = CreateCoreContainer(output);
        ActivateTomlMappers(coreContainer);

        IContainer modulesContainer = CreateModulesContainer(coreContainer);
        ActivateTomlMappers(modulesContainer);

        coreContainer.ResolveMany<IAutoActivate>();
        modulesContainer.ResolveMany<IAutoActivate>();

        Container = modulesContainer;
    }
    
    public static AppEngine Build(in string[] args, in TextWriter output)
    {
        return new AppEngine(args, output);
    }

    public void Dispose()
    {
        Container.Dispose();
    }

    public void Start()
    {
        foreach (IEntryPoint entryPoint in Container.ResolveMany<IEntryPoint>())
        {
            _logger.LogInformation("Running entry point '{entryPoint}'.", entryPoint.GetType());
            Debugger.TryInvoke(entryPoint.Run, $"Failed to run entry point '{entryPoint.GetType()}'");
        }
    }

    public static ILogger CreateLogger<T>()
    {
        return _loggerFactory.CreateLogger<T>();
    }
    
    public static ILogger CreateLogger(Type type)
    {
        return _loggerFactory.CreateLogger(type);
    }
    
    private static ILogger CreateLogger(Request request)
    {
        return _loggerFactory.CreateLogger(request.Parent.ImplementationType);
    }
    
    private static IModulePathService GetModulePathService(Request request)
    {
        return _modulePathServices[request.Parent.ImplementationType.Assembly];
    }
    
    private static void BuildLoggerFactory(ILoggingBuilder builder)
    {
        builder.AddProvider(_logListener);
        
        builder.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            options.ColorBehavior = LoggerColorBehavior.Enabled;
        });

        builder.AddFile(options =>
        {
            options.RootPath = AppContext.BaseDirectory;
            options.BasePath = "logs";
            options.DateFormat = "yyyy-MM-dd";
            options.IncludeScopes = true;
#if DEBUG
            options.FileAccessMode = LogFileAccessMode.KeepOpenAndAutoFlush;
#else
            options.FileAccessMode = LogFileAccessMode.KeepOpen;
#endif
            options.Files =
            [
                new LogFileOptions
                {
                    Path = $"{DateTime.Now:yyyy-MM-dd_HHmm-ss}.log",
                },
                new LogFileOptions
                {
                    Path = "latest.log",
                    MaxFileSize = 10_000_000,
                },
            ];
        });
    }

    private IContainer CreateCoreContainer(in TextWriter output)
    {
        IContainer container = new Container();

        container.RegisterInstance<TextWriter>(output);
        container.RegisterInstance<ILogListener>(_logListener);

        container.Register<IModulesLoader, ModulesLoader>(Reuse.Singleton);

        container.Register(Made.Of(() => CreateLogger(Arg.Index<Request>(0)), request => request));

        container.Register<ConfigurationProvider>(Reuse.Singleton);

        container.Register<IFileService, FileService>(Reuse.Singleton);
        container.RegisterMany<TomlParser<Language>>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterMany<TomlParser<ModuleOptions>>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterMany<TomlParser<ModuleManifest>>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);

        container.RegisterMany<PathTomlMapper>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterMany<PathInterfaceTomlMapper>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);

        container.RegisterDelegate(SmartFormatterProvider.Resolve);
        container.Register<ILocalizationProvider, Localization>(Reuse.Singleton);
        container.Register<IFormatter, LocalizationFormatter>(Reuse.Singleton);
        container.RegisterDelegate<IReadOnlyCollection<Language>>(() => container.Resolve<ConfigurationProvider>().GetLanguages().Select(languageFile => languageFile.Value).ToList());

        ValidateContainerOrDie(container);
        return container;
    }

    private IContainer CreateModulesContainer(IContainer parentContainer)
    {
        IContainer container = parentContainer.With();
        parentContainer.Resolve<IModulesLoader>().Load(AssemblyHookCallback);

        container.Register<CommandParser>(Reuse.Singleton, made: Made.Of(() => new CommandParser(Arg.Index<char>(0), Arg.Of<Command[]>()), _ => '\0'));
        container.Register<IModulePathService>(Made.Of(() => GetModulePathService(Arg.Index<Request>(0)), request => request));
        
        ValidateContainerOrDie(container);
        return container;

        void AssemblyHookCallback(ParsedFile<ModuleManifest> manifestFile, Assembly assembly)
        {
            RegisterEventProcessors(assembly, container);
            RegisterSerializers(assembly, container);
            RegisterCommands(assembly, container);
            RegisterDryIocModules(assembly, container);

            if (_modulePathServices.ContainsKey(manifestFile.Value.ID))
            {
                return;
            }

            var modulePathService = new ModulePathService(manifestFile);
            _modulePathServices.Add(manifestFile.Value.ID, assembly, modulePathService);
        }
    }

    private static void ValidateContainerOrDie(IContainer container)
    {
        KeyValuePair<ServiceInfo, ContainerException>[] errors = container.Validate();
        if (errors.Length <= 0)
        {
            return;
        }

        foreach (KeyValuePair<ServiceInfo, ContainerException> error in errors)
        {
            _logger.LogError(error.Value, "There was an error validating a container (service: {service}).", error.Key);
        }
        Environment.Exit((int)ExitCode.BadDependency);
    }

    private static void RegisterSerializers(Assembly assembly, IContainer container)
    {
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract)
            {
                continue;
            }

            Type[] interfaces = type.GetInterfaces();
            if (interfaces.Length == 0)
            {
                continue;
            }

            foreach (Type interfaceType in interfaces)
            {
                if (!interfaceType.IsGenericType)
                {
                    continue;
                }

                Type genericTypeDef = interfaceType.GetGenericTypeDefinition();
                if (genericTypeDef != typeof(ISerializer<>))
                {
                    continue;
                }
                
                container.RegisterMany(serviceTypes: interfaces, implType: type, reuse: Reuse.Singleton);
                break;
            }
        }
    }

    private static void RegisterEventProcessors(Assembly assembly, IContainer container)
    {
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract)
            {
                continue;
            }

            if (!type.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEventProcessor<>)))
            {
                continue;
            }

            container.RegisterMany(serviceTypes: type.GetInterfaces(), implType: type, reuse: Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        }
    }

    private static void RegisterCommands(Assembly assembly, IContainer container)
    {
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract)
            {
                continue;
            }

            if (!type.IsAssignableTo<Command>())
            {
                continue;
            }

            container.Register(typeof(Command), type, reuse: Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
            _logger.LogInformation("Registered command of type: {type}.", type);
        }
    }

    private static void RegisterDryIocModules(Assembly assembly, IContainer container)
    {
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract)
            {
                continue;
            }

            if (!typeof(IDryIocInjector).IsAssignableFrom(type))
            {
                continue;
            }

            var containerModule = (IDryIocInjector)Activator.CreateInstance(type)!;
            containerModule.Inject(container);
        }
    }

    private static void ActivateTomlMappers(IContainer container)
    {
        foreach (ITomlMapper mapper in container.ResolveMany<ITomlMapper>())
        {
            mapper.Register();
        }
    }
}