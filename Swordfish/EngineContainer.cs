using DryIoc;
using JoltPhysicsSharp;
using Reef;
using Shoal.Extensions.Swordfish;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Swordfish.Diagnostics.SilkNET.OpenGL;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.Jolt;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Pipelines;
using Swordfish.Graphics.SilkNET.OpenGL.Renderers;
using Swordfish.Input;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using Swordfish.UI;
using Swordfish.UI.Reef;
using Texture = Swordfish.Graphics.Texture;

namespace Swordfish;

public class EngineContainer(in IWindow window, in SynchronizationContext mainThreadContext)
{
    private readonly IWindow _window = window;
    private readonly SynchronizationContext _mainThreadContext = mainThreadContext;

    public void Register(IContainer container)
    {
        IInputContext inputContext = _window.CreateInput();
        GL gl = _window.CreateOpenGL();
        
        container.RegisterInstance<GL>(gl);
        container.RegisterMany<GLDebug>(Reuse.Singleton);
        container.RegisterInstance<IWindow>(_window);
        container.Register<GLContext>(Reuse.Singleton);
        container.Register<IWindowContext, SilkWindowContext>(Reuse.Singleton);
        
        container.RegisterMany<GLRenderContext>(Reuse.Singleton);
        
        container.Register<IRenderPipeline, ForwardPlusRenderingPipeline<ILightRenderStage>>(Reuse.Singleton);
        container.Register<GLInstancedRenderer>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterMapping<ILightRenderStage, GLInstancedRenderer>();
        container.RegisterMapping<IRenderStage, GLInstancedRenderer>();
        
        container.Register<IRenderPipeline, ForwardRenderingPipeline<IUnlitRenderStage>>(Reuse.Singleton);
        container.Register<GLLineRenderer>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterMapping<ILineRenderer, GLLineRenderer>();
        container.RegisterMapping<IUnlitRenderStage, GLLineRenderer>();
        container.RegisterMapping<IRenderStage, GLLineRenderer>();
        container.Register<JoltDebugRenderer>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterMapping<IUnlitRenderStage, JoltDebugRenderer>();
        container.RegisterMapping<IRenderStage, JoltDebugRenderer>();
        container.RegisterMapping<DebugRenderer, JoltDebugRenderer>();
        container.Register<GLScreenSpaceRenderer>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterMapping<IUnlitRenderStage, GLScreenSpaceRenderer>();
        container.RegisterMapping<IRenderStage, GLScreenSpaceRenderer>();
        container.Register<ReefRenderer>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterMapping<IUnlitRenderStage, ReefRenderer>();
        container.RegisterMapping<IRenderStage, ReefRenderer>();
        
        container.Register<ReefContext>(Reuse.Singleton);
        container.Register<IUIContext, ImGuiContext>(Reuse.Singleton);

        container.RegisterInstance<SynchronizationContext>(_mainThreadContext);
        
        container.RegisterMany<ECSContext>(Reuse.Singleton);
        container.Register<IEntitySystem, ChildSystem>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterMany<JoltPhysicsSystem>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation, nonPublicServiceTypes: true);
        container.Register<IEntitySystem, MeshRendererSystem>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.Register<IEntitySystem, RectRendererSystem>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);

        container.RegisterInstance<IInputContext>(inputContext);
        container.Register<IInputService, SilkInputService>(Reuse.Singleton);
        container.Register<IShortcutService, ShortcutService>(Reuse.Singleton);

        container.Register<IFileParser, GlslParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.Register<IFileParser, TextureParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.Register<IFileParser, TextureArrayParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.Register<IFileParser, ObjParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.Register<IFileParser, LegacyVoxelObjectParser>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        container.RegisterMany<TomlParser<MaterialDefinition>>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);

        container.RegisterMany<TextureDatabase>(Reuse.Singleton);
        container.RegisterMany<ShaderDatabase>(Reuse.Singleton);
        container.RegisterMany<MeshDatabase>(Reuse.Singleton);
        container.RegisterMany<MaterialDatabase>(Reuse.Singleton);
        
        var debugSettings = new DebugSettings();
        debugSettings.Stats.Set(true);

        container.RegisterInstance<DebugSettings>(debugSettings);
        
        container.RegisterConfig<RenderSettings>(file: "render.toml");
        container.RegisterConfig<WindowSettings>(file: "window.toml");
        
        container.RegisterDataBinding<AntiAliasing>();
        container.RegisterDataBinding<WindowMode>();
    }
}