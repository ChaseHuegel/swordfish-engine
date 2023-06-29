using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.Demo.ECS;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
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
        Vector4.One,
        Vector4.One,
        Vector4.One,
        Vector4.One
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
        // TestUI.CreateCanvas();
        // TestECS.Populate(ECSContext);
        // CreateTestEntities();
        // CreateStressTest();
        CreateShipTest();

        Benchmark.Log();
    }

    private void CreateStressTest()
    {
        var empty = new Brick(0);
        var solid = new Brick(1);

        BrickGrid grid = new(16);
        using (Benchmark.StartNew(nameof(Demo), nameof(CreateStressTest), "_CreateBrickGrid"))
        {
            const int size = 200;
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            for (int z = 0; z < size; z++)
            {
                grid.Set(x, y, z, solid);
            }
        }

        var triangles = new List<uint>();
        var vertices = new List<Vector3>();
        var colors = new List<Vector4>();
        var uv = new List<Vector3>();
        var normals = new List<Vector3>();

        var cube = new Cube();
        var slope = new Slope();
        using (Benchmark.StartNew(nameof(Demo), nameof(CreateStressTest), "_BuildBrickGridMesh"))
        {
            BuildBrickGridMesh(grid, -grid.CenterOfMass - new Vector3(0.5f));
        }

        void BuildBrickGridMesh(BrickGrid gridToBuild, Vector3 offset = default)
        {
            for (int nX = 0; nX < gridToBuild.NeighborGrids.GetLength(0); nX++)
            for (int nY = 0; nY < gridToBuild.NeighborGrids.GetLength(1); nY++)
            for (int nZ = 0; nZ < gridToBuild.NeighborGrids.GetLength(2); nZ++)
            {
                if (gridToBuild.NeighborGrids[nX, nY, nZ] != null)
                    BuildBrickGridMesh(gridToBuild.NeighborGrids[nX, nY, nZ], new Vector3(nX - 1, nY - 1, nZ - 1) * gridToBuild.DimensionSize + offset);
            }

            for (int x = 0; x < gridToBuild.DimensionSize; x++)
            for (int y = 0; y < gridToBuild.DimensionSize; y++)
            for (int z = 0; z < gridToBuild.DimensionSize; z++)
            {
                var brick = gridToBuild.Bricks[x, y, z];
                var quaternion = brick.GetQuaternion();
                if (!brick.Equals(empty) &&
                    (gridToBuild.Get(x + 1, y, z).Equals(empty)
                    || gridToBuild.Get(x - 1, y, z).Equals(empty)
                    || gridToBuild.Get(x, y + 1, z).Equals(empty)
                    || gridToBuild.Get(x, y - 1, z).Equals(empty)
                    || gridToBuild.Get(x, y, z + 1).Equals(empty)
                    || gridToBuild.Get(x, y, z - 1).Equals(empty))
                )
                {
                    Mesh brickMesh = brick.ID == 1 ? cube : slope;
                    colors.AddRange(brickMesh.Colors);
                    normals.AddRange(brickMesh.Normals);
                    uv.AddRange(brickMesh.UV);

                    foreach (uint tri in brickMesh.Triangles)
                        triangles.Add(tri + (uint)vertices.Count);

                    foreach (Vector3 vertex in brickMesh.Vertices)
                        vertices.Add(Vector3.Transform(vertex - new Vector3(0.5f), quaternion) + new Vector3(0.5f) + new Vector3(x, y, z) + offset);
                }
            }
        }

        var mesh = new Mesh(triangles.ToArray(), vertices.ToArray(), colors.ToArray(), uv.ToArray(), normals.ToArray());

        var shader = new Shader("textured", LocalPathService.Shaders.At("textured.glsl"));
        var texture = new Texture("metal_panel", LocalPathService.Textures.At("block/metal_panel.png"));
        var material = new Material(shader, texture);

        var renderOptions = new RenderOptions {
            DoubleFaced = false
        };

        var renderer = new MeshRenderer(mesh, material, renderOptions);

        ECSContext.EntityBuilder
            .Attach(new IdentifierComponent("Brick Entity", "bricks"), IdentifierComponent.DefaultIndex)
            .Attach(new TransformComponent(new Vector3(0, 0, 300), Vector3.Zero), TransformComponent.DefaultIndex)
            .Attach(new MeshRendererComponent(renderer), MeshRendererComponent.DefaultIndex)
            .Build();
    }

    private void CreateShipTest()
    {
        var empty = new Brick(0);
        var solid = new Brick(1);

        BrickGrid grid = new(16);
        using (Benchmark.StartNew(nameof(Demo), nameof(CreateShipTest), "_CreateBrickGrid"))
        {
            TryLoadVoxelObject("mainMenuVoxObj.svo", out grid!);
        }

        var triangles = new List<uint>();
        var vertices = new List<Vector3>();
        var colors = new List<Vector4>();
        var uv = new List<Vector3>();
        var normals = new List<Vector3>();

        var cube = new Cube();
        var slope = new Slope();
        using (Benchmark.StartNew(nameof(Demo), nameof(CreateShipTest), "_BuildBrickGridMesh"))
        {
            BuildBrickGridMesh(grid, -grid.CenterOfMass - new Vector3(0.5f));
        }

        void BuildBrickGridMesh(BrickGrid gridToBuild, Vector3 offset = default)
        {
            for (int nX = 0; nX < gridToBuild.NeighborGrids.GetLength(0); nX++)
            for (int nY = 0; nY < gridToBuild.NeighborGrids.GetLength(1); nY++)
            for (int nZ = 0; nZ < gridToBuild.NeighborGrids.GetLength(2); nZ++)
            {
                if (gridToBuild.NeighborGrids[nX, nY, nZ] != null)
                    BuildBrickGridMesh(gridToBuild.NeighborGrids[nX, nY, nZ], new Vector3(nX - 1, nY - 1, nZ - 1) * gridToBuild.DimensionSize + offset);
            }

            for (int x = 0; x < gridToBuild.DimensionSize; x++)
            for (int y = 0; y < gridToBuild.DimensionSize; y++)
            for (int z = 0; z < gridToBuild.DimensionSize; z++)
            {
                var brick = gridToBuild.Bricks[x, y, z];
                if (!brick.Equals(empty) &&
                    (gridToBuild.Get(x + 1, y, z).Equals(empty)
                    || gridToBuild.Get(x - 1, y, z).Equals(empty)
                    || gridToBuild.Get(x, y + 1, z).Equals(empty)
                    || gridToBuild.Get(x, y - 1, z).Equals(empty)
                    || gridToBuild.Get(x, y, z + 1).Equals(empty)
                    || gridToBuild.Get(x, y, z - 1).Equals(empty))
                )
                {
                    Mesh brickMesh = brick.ID == 1 ? cube : slope;
                    colors.AddRange(brickMesh.Colors);
                    normals.AddRange(brickMesh.Normals);
                    uv.AddRange(brickMesh.UV);

                    foreach (uint tri in brickMesh.Triangles)
                        triangles.Add(tri + (uint)vertices.Count);

                    foreach (Vector3 vertex in brickMesh.Vertices)
                        vertices.Add(Vector3.Transform(vertex - new Vector3(0.5f), brick.GetQuaternion()) + new Vector3(0.5f) + new Vector3(x, y, z) + offset);
                }
            }
        }

        var mesh = new Mesh(triangles.ToArray(), vertices.ToArray(), colors.ToArray(), uv.ToArray(), normals.ToArray());

        var shader = new Shader("textured", LocalPathService.Shaders.At("textured.glsl"));
        var texture = new Texture("metal_panel", LocalPathService.Textures.At("block/metal_panel.png"));
        var material = new Material(shader, texture);

        var renderOptions = new RenderOptions {
            DoubleFaced = false
        };

        var renderer = new MeshRenderer(mesh, material, renderOptions);

        ECSContext.EntityBuilder
            .Attach(new IdentifierComponent("Ship Entity", "bricks"), IdentifierComponent.DefaultIndex)
            .Attach(new TransformComponent(new Vector3(0, 0, 20), Vector3.Zero), TransformComponent.DefaultIndex)
            .Attach(new MeshRendererComponent(renderer), MeshRendererComponent.DefaultIndex)
            .Build();
    }

    private void CreateTestEntities()
    {
        var shader = new Shader("textured", LocalPathService.Shaders.At("textured.glsl"));

        var astronautTexture = new Texture("astronaut", LocalPathService.Textures.At("astronaut.png"));
        var chortTexture = new Texture("chort", LocalPathService.Textures.At("chort.png"));
        var hubertTexture = new Texture("hubert", LocalPathService.Textures.At("hubert.png"));
        var haroldTexture = new Texture("harold", LocalPathService.Textures.At("harold.png"));
        var melvinTexture = new Texture("melvin", LocalPathService.Textures.At("melvin.png"));
        var womanTexture = new Texture("woman", LocalPathService.Textures.At("woman.png"));

        var astronautMaterial = new Material(shader, astronautTexture);
        var chortMaterial = new Material(shader, chortTexture);
        var hubertMaterial = new Material(shader, hubertTexture);
        var haroldMaterial = new Material(shader, haroldTexture);
        var melvinMaterial = new Material(shader, melvinTexture);
        var womanMaterial = new Material(shader, womanTexture);

        var mesh = new Mesh(Triangles, Vertices, Colors, UV, Normals);

        var renderOptions = new RenderOptions
        {
            DoubleFaced = true
        };

        var random = new Randomizer();

        int index = 0;
        for (int x = 0; x < 20; x++)
        {
            for (int y = 0; y < 20; y++)
            {
                for (int z = 0; z < 30; z++)
                {
                    var material = random.Select(astronautMaterial, chortMaterial, hubertMaterial, haroldMaterial, melvinMaterial, womanMaterial);

                    ECSContext.EntityBuilder
                        .Attach(new IdentifierComponent($"{material.Textures[0].Name}{index++}", null), IdentifierComponent.DefaultIndex)
                        .Attach(new TransformComponent(new Vector3(x - 10, y - 7, z + 15), Vector3.Zero), TransformComponent.DefaultIndex)
                        .Attach(new MeshRendererComponent(new MeshRenderer(mesh, material, renderOptions)), MeshRendererComponent.DefaultIndex)
                        .Build();
                }
            }
        }
    }

    public override void Unload()
    {
        Debugger.Log("Dumping the log.");
        Debugger.Dump();
    }

    private bool TryLoadVoxelObject(string fileName, out BrickGrid? brickGrid)
    {
        IPath path = LocalPathService.Resources.At("saves").At(fileName);

        if (!path.FileExists())
        {
            brickGrid = null;
            return false;
        }

        using (Stream stream = FileService.Open(path))
        using (StreamReader file = new(stream))
        {
            string name = file.ReadLine()!;

            string[] parts = file.ReadLine()!.Split(',');
            int chunksX = int.Parse(parts[0]);
            int chunksY = int.Parse(parts[1]);
            int chunksZ = int.Parse(parts[2]);

            brickGrid = new BrickGrid(16);

            while (file.EndOfStream == false)
            {
                Brick brick = new(0);
                int x = 0, y = 0, z = 0;

                string entry = file.ReadLine()!;
                string[] sections = entry.Split('/');

                for (int i = 0; i < sections.Length; i++)
                {
                    string[] section = sections[i].Split(':');
                    string tag = section[0];
                    string value = section[1];

                    switch (tag)
                    {
                        case "v":
                            if (value.Contains("SLOPE"))
                                brick = new Brick(2);
                            else
                                brick = new Brick(1);
                            break;

                        case "p":
                            parts = value.Split(',');
                            x = int.Parse(parts[0]);
                            y = int.Parse(parts[1]);
                            z = int.Parse(parts[2]);
                            break;

                        case "r":
                            brick.Rotation = (Direction)Enum.Parse(typeof(Direction), value);
                            break;

                        case "o":
                            brick.Orientation = (Direction)Enum.Parse(typeof(Direction), value);
                            break;
                    }
                }

                if (brick.ID > 0 && x >= 0 && y >= 0 && z >= 0)
                    brickGrid.Set(x, y, z, brick);
            }
        }


        return brickGrid.Count > 0;
    }
}