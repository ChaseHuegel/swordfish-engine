using System.Reflection;
using Ninject;
using Swordfish.Demo.ECS;
using Swordfish.Demo.UI;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET;
using Swordfish.Library.BehaviorTrees;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Demo;

public class Demo : Mod
{
    public override string Name => "Swordfish Demo";
    public override string Description => "A demo of the Swordfish engine.";

    private static readonly float[] Vertices =
    {
        //X      Y       Z      R  G  B  A
         0.5f,   0.5f,   0.0f,  1, 0, 0, 1,
         0.5f,  -0.5f,   0.0f,  0, 0, 0, 1,
        -0.5f,  -0.5f,   0.0f,  0, 0, 1, 1,
        -0.5f,   0.5f,   0.5f,  0, 0, 0, 1
    };

    private static readonly uint[] Indices =
    {
        3, 1, 0,
        3, 2, 1
    };

    public override void Load()
    {
        IECSContext ecsContext = SwordfishEngine.Kernel.Get<IECSContext>();
        DemoComponent.Index = ecsContext.BindComponent<DemoComponent>();
    }

    public override void Initialize()
    {
        TestUI.CreateCanvas();
        TestECS.Populate();
        Benchmark.Log();

        IRenderContext renderContext = SwordfishEngine.Kernel.Get<IRenderContext>();
        RenderTarget renderTarget = new(Vertices, Indices, Shader.LoadFrom(LocalPathService.Shaders.At("shader.frag")));
        renderContext.Bind(renderTarget);
    }

    public override void Unload()
    {
        Debugger.Log("Dumping the log.");
        Debugger.Dump();
    }
}