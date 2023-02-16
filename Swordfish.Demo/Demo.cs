using System;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
using Swordfish.Util;
using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Demo;

public class Demo : Mod
{
    public override string Name => "Swordfish Demo";
    public override string Description => "A demo of the Swordfish engine.";

    private static readonly float[] VertexData =
    {
        //X     Y     Z     R   G   B   A   UX  VY  UZ
         0.5f,  0.5f, 0.0f, 1f, 0f, 0f, 1f, 1f, 0f, 0f,
         0.5f, -0.5f, 0.0f, 0f, 1f, 0f, 1f, 1f, 1f, 0f,
        -0.5f, -0.5f, 0.0f, 0f, 0f, 1f, 1f, 0f, 1f, 0f,
        -0.5f,  0.5f, 0.0f, 1f, 0f, 0f, 1f, 0f, 0f, 0f
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

        RenderTarget = new RenderTarget(
            VertexData,
            Indices,
            Shader.LoadFrom(LocalPathService.Shaders.At("textured.glsl")),
            Texture.LoadFrom(LocalPathService.Textures.At("astronaut.png"))
        )
        {
            Transform = {
                Position = new Vector3(0, 0, 1)
            }
        };
    }

    private RenderTarget RenderTarget;
    public override void Initialize()
    {
        IRenderContext renderContext = SwordfishEngine.Kernel.Get<IRenderContext>();
        IWindowContext windowContext = SwordfishEngine.Kernel.Get<IWindowContext>();

        renderContext.Bind(RenderTarget);
        windowContext.Update += OnUpdate;

        // TestUI.CreateCanvas();
        TestECS.Populate();
        Benchmark.Log();
    }

    private void OnUpdate(double delta)
    {
        RenderTarget.Transform.Rotation += new Vector3(5 * (float)delta, 5 * (float)delta, 0);
    }

    public override void Unload()
    {
        Debugger.Log("Dumping the log.");
        Debugger.Dump();
    }
}