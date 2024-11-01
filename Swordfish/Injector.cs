using DryIoc;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Shoal.DependencyInjection;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.Jolt;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Renderers;
using Swordfish.Input;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using Swordfish.UI;

namespace Swordfish;

// ReSharper disable once UnusedType.Global
public class Injector : IDryIocInjector
{
    public void Inject(IContainer resolver)
    {
        var enginePathService = new PathService();
        IInputContext inputContext = Program.MainWindow.CreateInput();
        GL gl = Program.MainWindow.CreateOpenGL();

        resolver.RegisterInstance<GL>(gl);
        resolver.RegisterInstance<IWindow>(Program.MainWindow);
        resolver.RegisterInstance<SynchronizationContext>(Program.MainThreadContext);
        resolver.Register<GLContext>(Reuse.Singleton);
        resolver.Register<IWindowContext, SilkWindowContext>(Reuse.Singleton);
        resolver.RegisterMany<GLRenderContext>(Reuse.Singleton);
        resolver.Register<IRenderStage, GLInstancedRenderer>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        resolver.Register<IUIContext, ImGuiContext>(Reuse.Singleton);
        resolver.RegisterMany<GLLineRenderer>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        resolver.Register<IRenderStage, JoltDebugRenderer>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);

        resolver.RegisterMany<ECSContext>(Reuse.Singleton);
        resolver.Register<IEntitySystem, ChildSystem>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        resolver.RegisterMany<JoltPhysicsSystem>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation, nonPublicServiceTypes: true);
        resolver.Register<IEntitySystem, MeshRendererSystem>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);

        resolver.RegisterInstance<IInputContext>(inputContext);
        resolver.Register<IInputService, SilkInputService>(Reuse.Singleton);
        resolver.Register<IShortcutService, ShortcutService>(Reuse.Singleton);

        resolver.RegisterInstance<IPathService>(enginePathService);
        resolver.Register<IFileService, FileService>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        resolver.Register<IFileParser, GlslParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        resolver.Register<IFileParser, TextureParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        resolver.Register<IFileParser, TextureArrayParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        resolver.Register<IFileParser, OBJParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        resolver.Register<IFileParser, LegacyVoxelObjectParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);

        var renderSettings = new RenderSettings();
        var debugSettings = new DebugSettings();
        debugSettings.Stats.Set(true);

        resolver.RegisterInstance<RenderSettings>(renderSettings);
        resolver.RegisterInstance<DebugSettings>(debugSettings);
    }
}