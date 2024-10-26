using Swordfish.AppEngine.Globalization;
using Swordfish.Library.Events;
using Swordfish.Library.Serialization;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Serialization.Toml.Mappers;

namespace Swordfish.AppEngine;

// ReSharper disable once UnusedType.Global
public sealed class Engine : IDisposable
{
    private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    private static readonly ILogger _logger = CreateLogger<Engine>();

    // ReSharper disable once MemberCanBePrivate.Global
    public IContainer? Container { get; private set; }

    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once UnusedParameter.Global
    public void Start(string[] args)
    {
        IContainer coreContainer = CreateCoreContainer();
        ActivateTomlMappers(coreContainer);

        IContainer modulesContainer = CreateModulesContainer(coreContainer);
        ActivateTomlMappers(modulesContainer);

        Container = modulesContainer;
    }

    public void Dispose()
    {
        Container?.Dispose();
    }

    private static IContainer CreateCoreContainer()
    {
        IContainer container = new Container();

        container.RegisterInstance(Console.Out);

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

    private static IContainer CreateModulesContainer(IContainer parentContainer)
    {
        IContainer container = parentContainer.With();
        parentContainer.Resolve<IModulesLoader>().Load(HookCallback);

        container.Register<CommandParser>(Reuse.Singleton, made: Made.Of(() => new CommandParser(Arg.Index<char>(0), Arg.Of<Command[]>()), _ => '\0'));

        ValidateContainerOrDie(container);
        return container;

        void HookCallback(Assembly assembly)
        {
            RegisterEventProcessors(assembly, container);
            RegisterSerializers(assembly, container);
            RegisterCommands(assembly, container);
            RegisterDryIocModules(assembly, container);
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
        Environment.Exit(1);
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

            if (!typeof(IDryIocModule).IsAssignableFrom(type))
            {
                continue;
            }

            var containerModule = (IDryIocModule)Activator.CreateInstance(type)!;
            containerModule.Load(container);
        }
    }

    private static void ActivateTomlMappers(IContainer container)
    {
        foreach (ITomlMapper mapper in container.ResolveMany<ITomlMapper>())
        {
            mapper.Register();
        }
    }

    private static ILogger CreateLogger(Request request)
    {
        return _loggerFactory.CreateLogger(request.Parent.ImplementationType);
    }

    public static ILogger CreateLogger<T>()
    {
        return _loggerFactory.CreateLogger<T>();
    }
}