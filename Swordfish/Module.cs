using Ninject.Modules;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.UI;

namespace Swordfish;

public class Module : NinjectModule
{
    public override void Load()
    {
        Bind<IWindowContext>().To<SilkWindowContext>().InSingletonScope();
        Bind<IRenderContext>().To<OpenGLRenderer>().InSingletonScope();
        Bind<IUIContext>().To<ImGuiContext>().InSingletonScope();

        Bind<IPathService>().To<PathService>().InSingletonScope();
        Bind<IFileService>().To<FileService>().InSingletonScope();
        Bind<IPluginContext>().To<PluginContext>().InSingletonScope();

        Bind<IECSContext>().To<ECSContext>().InSingletonScope();
    }
}
