using System.Numerics;
using Swordfish.Demo.ECS;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
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

    private static IECSContext ECSContext;
    private static IRenderContext RenderContext;
    private IWindowContext WindowContext;
    private readonly IFileService FileService;

    private RenderTarget RenderTarget;


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
        RenderTarget = new RenderTarget(
            VertexData,
            Indices,
            FileService.Parse<ShaderProgram>(LocalPathService.Shaders.At("textured.glsl")),
            FileService.Parse<Texture>(LocalPathService.Textures.At("astronaut.png"))
        )
        {
            Transform = {
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
        RenderTarget.Transform.Rotation += new Vector3(5 * (float)delta, 5 * (float)delta, 0);
    }

    public override void Unload()
    {
        Debugger.Log("Dumping the log.");
        Debugger.Dump();
    }
}