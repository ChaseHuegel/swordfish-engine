using Microsoft.Extensions.Logging.Console;
using Shoal.CommandLine;
using Shoal.CrashHandling;
using Shoal.Extensions.Swordfish;
using Shoal.Globalization;
using Shoal.Logging;
using Shoal.Modularity;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Events;
using Swordfish.Library.Serialization;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Serialization.Toml.Mappers;
using Swordfish.Library.Util;

namespace Shoal;

public sealed class AppEngine : IDisposable
{
    private static readonly object _loggerLock = new();
    private static readonly LogListener _logListener = new();
    private static AppPaths? _configuredPaths;
    private static LoggingConfig? _loggingConfig;
    private static AsyncFileLoggerProvider? _fileLoggerProvider;
    private static int _globalHandlersRegistered;
    private static volatile ILoggerFactory? _loggerFactory;
    private static volatile ILogger? _logger;

    private static ILogger Logger => _logger ??= CreateLogger<AppEngine>();

    /// <summary>
    ///     Gets the active logging configuration loaded for the configured application paths.
    /// </summary>
    public static LoggingConfig LoggingConfig => _loggingConfig ?? LoggingConfig.CreateDefault();

    public IContainer Container { get; }

    private AppEngine(in string[] args, AppPaths appPaths, DryIocInjectCallback? dryIocInjectCallback = null)
    {
        ConfigurePaths(appPaths);
        Logger.LogInformation(
            "Using data root '{dataRoot}', config root '{configRoot}', and logs root '{logsRoot}'.",
            appPaths.DataRoot.Value,
            appPaths.ConfigRoot.Value,
            appPaths.LogsRoot.Value);

        CommandLineArgs commandLineArgs;
        try
        {
            var commandLineParser = new CommandLineParser();
            commandLineArgs = commandLineParser.Parse(args);

            Logger.LogInformation("Using command line arguments: {commandLineArgs}.", commandLineArgs);
        }
        catch (Exception ex)
        {
            commandLineArgs = CommandLineArgs.Empty;
            Logger.LogError(ex, "Failed to parse command line arguments.");
        }

        IContainer coreContainer = CreateCoreContainer(commandLineArgs, appPaths);
        ActivateTomlMappers(coreContainer);

        dryIocInjectCallback?.Invoke(coreContainer);

        IContainer modulesContainer = CreateModulesContainer(coreContainer, appPaths);
        ActivateTomlMappers(modulesContainer);

        //  Load languages
        coreContainer.Resolve<ConfigurationProvider>().GetLanguages();

        //  Auto activate things
        foreach (IAutoActivate _ in coreContainer.ResolveMany<IAutoActivate>()) { }
        foreach (IAutoActivate _ in modulesContainer.ResolveMany<IAutoActivate>()) { }

        Container = modulesContainer;
    }

    /// <summary>Configures the application paths used by logging before the first logger is created.</summary>
    public static void ConfigurePaths(AppPaths appPaths)
    {
        lock (_loggerLock)
        {
            if (_loggerFactory == null)
            {
                _configuredPaths = appPaths;
            }
        }
    }

    public static AppEngine Build(in string[] args, AppPaths appPaths)
    {
        return new AppEngine(args, appPaths);
    }

    public static AppEngine Build(in string[] args, AppPaths appPaths, DryIocInjectCallback dryIocInjectCallback)
    {
        return new AppEngine(args, appPaths, dryIocInjectCallback);
    }

    public void Dispose()
    {
        Container.Dispose();
        FlushLogs();
    }

    /// <summary>
    ///     Flushes all buffered file log entries to disk.
    /// </summary>
    public static void FlushLogs(TimeSpan? timeout = null)
    {
        _fileLoggerProvider?.Flush(timeout ?? TimeSpan.FromSeconds(2));
    }

    /// <summary>
    ///     Flushes and closes file logging cleanly during process shutdown.
    /// </summary>
    public static void ShutdownLogging()
    {
        _fileLoggerProvider?.Dispose();
    }

    public void Start()
    {
        foreach (IEntryPoint entryPoint in Container.ResolveMany<IEntryPoint>())
        {
            Logger.LogInformation("Running entry point '{entryPoint}'.", entryPoint.GetType());

            Result<Exception> result = Safe.Invoke(entryPoint.Run);
            if (!result)
            {
                Logger.LogError(result.Value, "Failed to run entry point '{entryPoint}'", entryPoint.GetType());
            }
        }
    }

    public static ILogger<T> CreateLogger<T>()
    {
        return GetLoggerFactory().CreateLogger<T>();
    }

    public static ILogger CreateLogger(Type type)
    {
        return GetLoggerFactory().CreateLogger(type);
    }

    private static ILogger CreateLogger(Request request)
    {
        ILoggerFactory factory = GetLoggerFactory();
        if (request.Parent?.ImplementationType == null)
        {
            return factory.CreateLogger("Default");
        }

        return factory.CreateLogger(request.Parent.ImplementationType);
    }

    private static ILoggerFactory GetLoggerFactory()
    {
        if (_loggerFactory != null)
        {
            return _loggerFactory;
        }

        lock (_loggerLock)
        {
            if (_loggerFactory == null)
            {
                AppPaths paths = _configuredPaths
                    ?? throw new InvalidOperationException("App paths must be configured before the first logger is created.");
                RegisterGlobalCrashHooks();
                _loggingConfig = LoggingConfig.Load(paths);
                _loggerFactory = LoggerFactory.Create(builder => BuildLoggerFactory(builder, paths, _loggingConfig));
            }

            return _loggerFactory;
        }
    }

    private static void RegisterGlobalCrashHooks()
    {
        if (Interlocked.Exchange(ref _globalHandlersRegistered, 1) != 0)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        string threadName = string.IsNullOrEmpty(Thread.CurrentThread.Name)
            ? $"Thread {Environment.CurrentManagedThreadId}"
            : $"{Thread.CurrentThread.Name} (ID {Environment.CurrentManagedThreadId})";

        string exceptionText = e.ExceptionObject is Exception ex
            ? ex.ToString()
            : e.ExceptionObject?.ToString() ?? "(unknown exception object)";

        CrashSession.RecordUnhandledException(threadName, exceptionText);

        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [CRT] [{threadName}] UnhandledException (IsTerminating={e.IsTerminating}):{Environment.NewLine}{exceptionText}";
        _fileLoggerProvider?.WriteEmergency(line);
        FlushLogs(TimeSpan.FromSeconds(2));
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.LogError(e.Exception, "Unobserved task exception.");
        FlushLogs(TimeSpan.FromSeconds(1));
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        FlushLogs(TimeSpan.FromSeconds(2));
    }

    private static void BuildLoggerFactory(ILoggingBuilder builder, AppPaths appPaths, LoggingConfig loggingConfig)
    {
        builder.SetMinimumLevel(loggingConfig.MinLevel);
        builder.AddProvider(_logListener);

        builder.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            options.ColorBehavior = LoggerColorBehavior.Enabled;
        });

        string logsDirectory = appPaths.LogsRoot.Value;
        LogRingBuffer? ringBuffer = null;
        string? ringPath = CrashSession.RingBufferPath;
        if (!string.IsNullOrWhiteSpace(ringPath))
        {
            try
            {
                ringBuffer = LogRingBuffer.CreateOrOpenWriter(ringPath);
            }
            catch
            {
                ringBuffer = null;
            }
        }

        _fileLoggerProvider = new AsyncFileLoggerProvider(
            loggingConfig,
            logsDirectory,
            ringBuffer,
            ownsRingBuffer: ringBuffer != null
        );

        CrashSession.SetLogPaths(_fileLoggerProvider.TimestampedLogPath, _fileLoggerProvider.LatestLogPath);
        builder.AddProvider(_fileLoggerProvider);
    }

    private IContainer CreateCoreContainer(CommandLineArgs args, AppPaths appPaths)
    {
        IContainer container = new Container();

        container.RegisterInstance<CommandLineArgs>(args);
        container.RegisterInstance(appPaths);
        container.RegisterInstance<LogListener>(_logListener);
        container.RegisterInstance<LoggingConfig>(LoggingConfig);

        container.Register<VirtualFileSystem>(Reuse.Singleton);
        container.Register<ModuleCatalog>(Reuse.Singleton);
        container.Register<IModulesLoader, ModulesLoader>(Reuse.Singleton);

        container.RegisterInstance(GetLoggerFactory(), setup: Setup.With(preventDisposal: true));
        MethodInfo loggerFactoryMethod = typeof(LoggerFactoryExtensions).GetMethod("CreateLogger", [typeof(ILoggerFactory)])!;
        container.Register(typeof(ILogger<>), made: Made.Of(req => loggerFactoryMethod.MakeGenericMethod(req.Parent.ImplementationType)));
        container.Register(Made.Of(() => CreateLogger( Arg.Index<Request>(0)), request => request));

        container.Register<ConfigurationProvider>(Reuse.Singleton);

        container.Register<IFileParseService, VirtualFileParseService>(Reuse.Transient);
        
        container.RegisterTomlParser<Language>();
        container.RegisterTomlParser<ModuleOptions>();
        container.RegisterTomlParser<ModuleManifest>();

        container.RegisterTomlMapper<PathTomlMapper>();
        container.RegisterDataBinding<PathInfo>();
        
        container.RegisterDataBinding<bool>();
        container.RegisterDataBinding<byte>();
        container.RegisterDataBinding<sbyte>();
        container.RegisterDataBinding<char>();
        container.RegisterDataBinding<decimal>();
        container.RegisterDataBinding<double>();
        container.RegisterDataBinding<float>();
        container.RegisterDataBinding<int>();
        container.RegisterDataBinding<uint>();
        container.RegisterDataBinding<nint>();
        container.RegisterDataBinding<long>();
        container.RegisterDataBinding<ulong>();
        container.RegisterDataBinding<short>();
        container.RegisterDataBinding<ushort>();
        
        container.RegisterDataBinding<bool?>();
        container.RegisterDataBinding<byte?>();
        container.RegisterDataBinding<sbyte?>();
        container.RegisterDataBinding<char?>();
        container.RegisterDataBinding<decimal?>();
        container.RegisterDataBinding<double?>();
        container.RegisterDataBinding<float?>();
        container.RegisterDataBinding<int?>();
        container.RegisterDataBinding<uint?>();
        container.RegisterDataBinding<nint?>();
        container.RegisterDataBinding<long?>();
        container.RegisterDataBinding<ulong?>();
        container.RegisterDataBinding<short?>();
        container.RegisterDataBinding<ushort?>();
        
        container.RegisterDataBinding<object>();
        container.RegisterDataBinding<string>();

        container.RegisterDelegate(SmartFormatterProvider.Resolve);
        container.Register<Localization>(Reuse.Singleton);
        container.RegisterMapping<ILocalization, Localization>();
        container.RegisterMapping<ILocalizationProvider, Localization>();
        container.Register<IFormatter, LocalizationFormatter>(Reuse.Singleton);
        container.RegisterDelegate<IReadOnlyCollection<Language>>(() => container.Resolve<ConfigurationProvider>().GetLanguages().Select(languageFile => languageFile.Value).ToList());

        ValidateContainerOrDie(container);
        return container;
    }

    private IContainer CreateModulesContainer(IContainer parentContainer, AppPaths appPaths)
    {
        var vfs = parentContainer.Resolve<VirtualFileSystem>();

        IContainer container = parentContainer.With();
        var loadedAssemblies = new List<Assembly>();
        parentContainer.Resolve<IModulesLoader>().Load((manifestFile, assemblies) =>
        {
            CrashSession.AddLoadedModule($"{manifestFile.Value.Name} ({manifestFile.Value.ID})");
            vfs.Mount(manifestFile.GetRootPath().At("assets"));
            loadedAssemblies.AddRange(assemblies);
        });

        if (appPaths.MountInVfs && appPaths.DataRoot.DirectoryExists())
        {
            Result mountResult = vfs.Mount(appPaths.DataRoot);
            if (!mountResult)
            {
                Logger.LogWarning("Failed to mount user data directory '{path}': {message}", appPaths.DataRoot.Value, mountResult.Message);
            }
        }

        foreach (Assembly assembly in loadedAssemblies)
        {
            RegisterEventProcessors(assembly, container);
            RegisterSerializers(assembly, container);
            RegisterCommands(assembly, container);
            RegisterDryIocModules(assembly, container);
        }

        container.Register<CommandParser>(Reuse.Singleton, made: Made.Of(() => new CommandParser(Arg.Index<char>(0), Arg.Of<Command[]>()), _ => '\0'));

        ValidateContainerOrDie(container);
        return container;
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
            Logger.LogError(error.Value, "There was an error validating a container (service: {service}).", error.Key);
        }

        FlushLogs(TimeSpan.FromSeconds(2));
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
            Logger.LogInformation("Registered command of type: {type}.", type);
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

            var injector = (IDryIocInjector)Activator.CreateInstance(type)!;
            injector.Inject(container);
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