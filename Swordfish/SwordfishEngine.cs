using System.Runtime.InteropServices;
using MicroResolver;
using Silk.NET.OpenGL;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Input;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.UI;

namespace Swordfish;

public static class SwordfishEngine
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AllocConsole();

    [DllImport("kernel32", SetLastError = true)]
    static extern bool AttachConsole(int dwProcessId);

    public static readonly Version? Version;

    public static readonly Kernel Kernel;

    static SwordfishEngine()
    {
        Version = typeof(SwordfishEngine).Assembly.GetName().Version;

        var engineResolver = ObjectResolver.Create();

        engineResolver.Register<IWindowContext, SilkWindowContext>(Lifestyle.Singleton);
        engineResolver.Register<IRenderContext, RenderContext>(Lifestyle.Singleton);
        engineResolver.Register<IUIContext, ImGuiContext>(Lifestyle.Singleton);

        engineResolver.Register<IECSContext, ECSContext>(Lifestyle.Singleton);

        engineResolver.Register<IPluginContext, PluginContext>(Lifestyle.Singleton);

        engineResolver.Register<IInputService, SilkInputService>(Lifestyle.Singleton);
        engineResolver.Register<IShortcutService, ShortcutService>(Lifestyle.Singleton);

        engineResolver.Register<IPathService, PathService>(Lifestyle.Singleton);
        engineResolver.Register<IFileService, FileService>(Lifestyle.Singleton);

        engineResolver.Compile();
        Kernel = new Kernel(engineResolver);
    }

    static void Main(string[] args)
    {
        if (args.Contains("-debug") && !AttachConsole(-1))
            AllocConsole();

        var mainWindow = Kernel.Get<IWindowContext>();
        mainWindow.Loaded += Start;
        mainWindow.Closed += Stop;
        mainWindow.Initialize();
    }

    private static void Start()
    {
        var pathService = Kernel.Get<IPathService>();
        var pluginContext = Kernel.Get<IPluginContext>();
        var ecsContext = Kernel.Get<IECSContext>();

        pluginContext.LoadFrom(pathService.Root);
        pluginContext.LoadFrom(pathService.Plugins, SearchOption.AllDirectories);
        pluginContext.LoadFrom(pathService.Mods, SearchOption.AllDirectories);

        ecsContext.Start();

        pluginContext.Initialize();
    }

    private static void Stop()
    {
        Kernel.Get<IPluginContext>().UnloadAll();
        Kernel.Get<IECSContext>().Stop();
    }
}
