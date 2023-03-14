using System.Numerics;
using Swordfish.Demo.ECS;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
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

    private readonly IECSContext ECSContext;
    private readonly IRenderContext RenderContext;
    private readonly IWindowContext WindowContext;
    private readonly IFileService FileService;

    private MeshRenderer? RenderTarget;

    public Demo(IECSContext ecsContext, IRenderContext renderContext, IWindowContext windowContext, IFileService fileService)
    {
        FileService = fileService;
        ECSContext = ecsContext;
        RenderContext = renderContext;
        WindowContext = windowContext;

        DemoComponent.Index = ecsContext.BindComponent<DemoComponent>();

        Benchmark.Log();
    }

    public override void Start()
    {
        var shader = FileService.Parse<Shader>(LocalPathService.Shaders.At("textured.glsl"));
        var texture = FileService.Parse<Texture>(LocalPathService.Textures.At("astronaut.png"));
        var material = new Material(shader, texture);
        var mesh = new Mesh();

        RenderTarget = new MeshRenderer(mesh, material)
        {
            Transform = new Transform
            {
                Position = new Vector3(0, 0, 1)
            }
        };

        RenderContext.Bind(RenderTarget);
        WindowContext.Update += OnUpdate;

        // TestUI.CreateCanvas();
        TestECS.Populate(ECSContext);
    }

    private void OnUpdate(double delta)
    {
        RenderTarget!.Transform.Rotate(new Vector3(5 * (float)delta, 5 * (float)delta, 0));
    }

    public override void Unload()
    {
        Debugger.Log("Dumping the log.");
        Debugger.Dump();
    }
}