using System;
using System.Drawing;
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

    private static readonly uint[] Triangles =
    {
        3, 1, 0,
        3, 2, 1
    };

    private static readonly Vector3[] Vertices =
    {
        new Vector3(0.5f,  0.5f, 0.0f),
        new Vector3(0.5f, -0.5f, 0.0f),
        new Vector3(-0.5f, -0.5f, 0.0f),
        new Vector3(-0.5f,  0.5f, 0.0f)
    };

    private static readonly Vector4[] Colors =
    {
        new Vector4(1f, 0f, 0f, 1f),
        new Vector4(0f, 1f, 0f, 1f),
        new Vector4(0f, 0f, 1f, 1f),
        new Vector4(1f, 0f, 0f, 1f)
    };

    private static readonly Vector3[] UV =
    {
        new Vector3(1f, 0f, 0f),
        new Vector3(1f, 1f, 0f),
        new Vector3(0f, 1f, 0f),
        new Vector3(0f, 0f, 0f)
    };

    private static readonly Vector3[] Normals =
    {
        new Vector3(1f),
        new Vector3(1f),
        new Vector3(1f),
        new Vector3(1f)
    };

    private readonly IECSContext ECSContext;
    private readonly IRenderContext RenderContext;
    private readonly IWindowContext WindowContext;
    private readonly IFileService FileService;

    private MeshRenderer? MeshRenderer;

    public Demo(IECSContext ecsContext, IRenderContext renderContext, IWindowContext windowContext, IFileService fileService)
    {
        FileService = fileService;
        ECSContext = ecsContext;
        RenderContext = renderContext;
        WindowContext = windowContext;

        DemoComponent.Index = ecsContext.BindComponent<DemoComponent>();
        ecsContext.BindSystem<RoundaboutSystem>();
    }

    public override void Start()
    {
        var shader = new Shader("textured", LocalPathService.Shaders.At("textured.glsl"));
        var texture = new Texture("astronaut", LocalPathService.Textures.At("astronaut.png"));
        var material = new Material(shader, texture);
        var mesh = new Mesh(Triangles, Vertices, Colors, UV, Normals);

        MeshRenderer = new MeshRenderer(mesh, material)
        {
            Transform =
            {
                Position = new Vector3(0, 0, 1)
            }
        };

        int index = 0;
        for (int x = 0; x < 20; x++)
        {
            for (int y = 0; y < 20; y++)
            {
                for (int z = 0; z < 30; z++)
                {
                    ECSContext.EntityBuilder
                        .Attach(new IdentifierComponent($"Astronaut{index++}", null), IdentifierComponent.DefaultIndex)
                        .Attach(new TransformComponent(new Vector3(x - 10, y - 7, z + 15), Vector3.Zero), TransformComponent.DefaultIndex)
                        .Attach(new MeshRendererComponent(new MeshRenderer(mesh, material)), MeshRendererComponent.DefaultIndex)
                        .Build();
                }
            }
        }

        // RenderContext.Bind(RenderTarget);
        WindowContext.Update += OnUpdate;

        // TestUI.CreateCanvas();
        // TestECS.Populate(ECSContext);

        Benchmark.Log();
    }

    private void OnUpdate(double delta)
    {
        // MeshRenderer!.Transform.Rotate(new Vector3(5 * (float)delta, 5 * (float)delta, 0));
        ECSContext.Update((float)delta);
    }

    public override void Unload()
    {
        Debugger.Log("Dumping the log.");
        Debugger.Dump();
    }
}