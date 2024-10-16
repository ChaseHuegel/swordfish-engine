using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Sockets;
using System.Numerics;
using LibNoise.Primitive;
using Swordfish.Bricks;
using Swordfish.Demo.ECS;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Library.Constraints;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Types.Constraints;
using Swordfish.UI.Elements;
using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Demo;

public partial class Demo : Mod
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
    private readonly IPhysics Physics;
    private readonly IInputService InputService;
    private readonly TextElement DebugText;

    public Demo(IECSContext ecsContext, IRenderContext renderContext, IWindowContext windowContext, IFileService fileService, IPhysics physics, IInputService inputService, ILineRenderer lineRenderer)
    {
        FileService = fileService;
        ECSContext = ecsContext;
        RenderContext = renderContext;
        WindowContext = windowContext;
        Physics = physics;
        InputService = inputService;

        DebugText = new TextElement("");
        CanvasElement myCanvas = new("Demo Debug Canvas")
        {
            Constraints = new RectConstraints
            {
                Anchor = ConstraintAnchor.TOP_RIGHT,
                X = new RelativeConstraint(0.2f),
                Y = new RelativeConstraint(0.1f),
                Width = new RelativeConstraint(0.1f),
                Height = new RelativeConstraint(0.1f)
            },
            Content = {
                new PanelElement("Demo Debug Panel")
                {
                    Constraints = new RectConstraints
                    {
                        Height = new FillConstraint()
                    },
                    Tooltip = new Tooltip
                    {
                        Help = true,
                        Text = "This is a panel for debugging in the demo."
                    },
                    Content = {
                        DebugText,
                    }
                }
            }
        };

        inputService.Clicked += OnClick;
        inputService.Scrolled += OnScroll;
        Physics.FixedUpdate += OnFixedUpdate;
        _positionGizmo = new PositionGizmo(lineRenderer, RenderContext.Camera.Get());
        _orientationGizmo = new OrientationGizmo(lineRenderer, RenderContext.Camera.Get());
        _scaleGizmo = new ScaleGizmo(lineRenderer, RenderContext.Camera.Get());

        DemoComponent.Index = ecsContext.BindComponent<DemoComponent>();
        // ecsContext.BindSystem<RoundaboutSystem>();
    }

    private PositionGizmo _positionGizmo;
    private OrientationGizmo _orientationGizmo;
    private ScaleGizmo _scaleGizmo;

    private void OnFixedUpdate(object? sender, EventArgs args)
    {
        Camera camera = RenderContext.Camera.Get();
        Vector2 cursorPos = InputService.CursorPosition;
        Ray ray = camera.ScreenPointToRay((int)cursorPos.X, (int)cursorPos.Y, (int)WindowContext.Resolution.X, (int)WindowContext.Resolution.Y);
        RaycastResult raycast = Physics.Raycast(ray * 1000);

        if (!raycast.Hit)
        {
            DebugText.Text = "";
            return;
        }

        DebugText.Text = $"Hovering {raycast.Entity.GetComponent<IdentifierComponent>()?.Name} ({raycast.Entity.Ptr})";

        if (!InputService.IsMouseHeld(MouseButton.LEFT))
        {
            return;
        }

        TransformComponent? transform = raycast.Entity.GetComponent<TransformComponent>();
        if (transform == null)
        {
            return;
        }

        float scroll = _scrollBuffer;
        transform.Position = raycast.Point;
        if (scroll != 0)
        {
            transform.Orientation = Quaternion.Multiply(transform.Orientation, Quaternion.CreateFromYawPitchRoll(15 * scroll * MathS.DegreesToRadians, 0, 0));
            _scrollBuffer -= scroll;
        }

        _positionGizmo.Render(transform);
        _orientationGizmo.Render(transform);
        _scaleGizmo.Render(transform);

        PhysicsComponent? physics = raycast.Entity.GetComponent<PhysicsComponent>();
        if (physics == null)
        {
            return;
        }

        physics.Velocity = Vector3.Zero;
        physics.Torque = Vector3.Zero;
    }

    private void OnClick(object? sender, ClickedEventArgs e)
    {
        if (e.MouseButton != MouseButton.LEFT)
        {
            return;
        }

        Camera camera = RenderContext.Camera.Get();
        Vector2 cursorPos = InputService.CursorPosition;
        Ray ray = camera.ScreenPointToRay((int)cursorPos.X, (int)cursorPos.Y, (int)WindowContext.Resolution.X, (int)WindowContext.Resolution.Y);
        RaycastResult raycast = Physics.Raycast(ray * 1000);

        if (!raycast.Hit)
        {
            return;
        }
    }

    private volatile float _scrollBuffer = 0f;
    private void OnScroll(object? sender, ScrolledEventArgs e)
    {
        Camera camera = RenderContext.Camera.Get();
        Vector2 cursorPos = InputService.CursorPosition;
        Ray ray = camera.ScreenPointToRay((int)cursorPos.X, (int)cursorPos.Y, (int)WindowContext.Resolution.X, (int)WindowContext.Resolution.Y);
        RaycastResult raycast = Physics.Raycast(ray * 1000);

        if (!raycast.Hit)
        {
            return;
        }

        if (!InputService.IsMouseHeld(MouseButton.LEFT))
        {
            return;
        }

        TransformComponent? transform = raycast.Entity.GetComponent<TransformComponent>();
        if (transform == null)
        {
            return;
        }

        _scrollBuffer += e.Delta;
    }

    public override void Start()
    {
        // TestUI.CreateCanvas();
        // TestECS.Populate(ECSContext);
        // CreateTestEntities();
        // CreateStressTest();
        // CreateShipTest();
        // CreateVoxelTest();
        // CreateTerrainTest();
        // CreateDonutDemo();
        CreatePhysicsTest();

        RenderContext.Camera.Get().Transform.Position = new Vector3(0, 5, 15);
        RenderContext.Camera.Get().Transform.Rotate(new Vector3(-30, 0, 0));

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
            BuildBrickGridMesh(grid, -grid.CenterOfMass);
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

        var shader = FileService.Parse<Shader>(LocalPathService.Shaders.At("lighted.glsl"));
        var texture = FileService.Parse<Texture>(LocalPathService.Textures.At("block/metal_panel.png"));
        var material = new Material(shader, texture);

        var renderOptions = new RenderOptions {
            DoubleFaced = false
        };

        var renderer = new MeshRenderer(mesh, material, renderOptions);

        ECSContext.EntityBuilder
            .Attach(new IdentifierComponent("Brick Entity", "bricks"), IdentifierComponent.DefaultIndex)
            .Attach(new TransformComponent(new Vector3(0, 0, 300), Quaternion.Identity), TransformComponent.DefaultIndex)
            .Attach(new MeshRendererComponent(renderer), MeshRendererComponent.DefaultIndex)
            .Build();
    }

    private void CreateShipTest()
    {
        var empty = new Brick(0);
        var solid = new Brick(1);

        BrickGrid grid = new(16);
        using (Benchmark.StartNew(nameof(Demo), nameof(CreateShipTest), "_LoadBrickGrid"))
        {
            grid = FileService.Parse<BrickGrid>(LocalPathService.Resources.At("saves").At("mainMenuVoxObj.svo"));
        }

        var triangles = new List<uint>();
        var vertices = new List<Vector3>();
        var colors = new List<Vector4>();
        var uv = new List<Vector3>();
        var normals = new List<Vector3>();

        var cube = new Cube();
        var slope = new Slope();
        var thruster = FileService.Parse<Mesh>(LocalPathService.Models.At("thruster_rocket.obj"));
        var thrusterBlock = FileService.Parse<Mesh>(LocalPathService.Models.At("thruster_rocket_internal.obj"));
        using (Benchmark.StartNew(nameof(Demo), nameof(CreateShipTest), "_BuildBrickGridMesh"))
        {
            BuildBrickGridMesh(grid, -grid.CenterOfMass);
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
                    Mesh brickMesh = MeshFromBrickID(brick.ID);
                    colors.AddRange(brickMesh.Colors);
                    uv.AddRange(brickMesh.UV);

                    Mesh MeshFromBrickID(int id)
                    {
                        return id switch
                        {
                            2 => slope,
                            3 => thruster,
                            4 => thrusterBlock,
                            _ => cube,
                        };
                    }

                    foreach (uint tri in brickMesh.Triangles)
                        triangles.Add(tri + (uint)vertices.Count);

                    foreach (Vector3 normal in brickMesh.Normals)
                        normals.Add(Vector3.Transform(normal, brick.GetQuaternion()));

                    foreach (Vector3 vertex in brickMesh.Vertices)
                        vertices.Add(Vector3.Transform(vertex, brick.GetQuaternion()) + new Vector3(x, y, z) + offset);
                }
            }
        }

        var mesh = new Mesh(triangles.ToArray(), vertices.ToArray(), colors.ToArray(), uv.ToArray(), normals.ToArray());

        var shader = FileService.Parse<Shader>(LocalPathService.Shaders.At("lighted.glsl"));
        var texture = FileService.Parse<Texture>(LocalPathService.Textures.At("block/metal_panel.png"));
        var material = new Material(shader, texture);

        var renderOptions = new RenderOptions {
            DoubleFaced = false,
            Wireframe = false
        };

        var renderer = new MeshRenderer(mesh, material, renderOptions);

        ECSContext.EntityBuilder
            .Attach(new IdentifierComponent("Ship Entity", "bricks"), IdentifierComponent.DefaultIndex)
            .Attach(new TransformComponent(new Vector3(0, 0, 20), Quaternion.Identity), TransformComponent.DefaultIndex)
            .Attach(new MeshRendererComponent(renderer), MeshRendererComponent.DefaultIndex)
            .Build();
    }

    private void CreateVoxelTest()
    {
        BrickGrid grid = new(16);
        using (Benchmark.StartNew(nameof(Demo), nameof(CreateShipTest), "_LoadBrickGrid"))
        {
            grid = FileService.Parse<BrickGrid>(LocalPathService.Resources.At("saves").At("mainMenuVoxObj.svo"));
        }

        var shader = FileService.Parse<Shader>(LocalPathService.Shaders.At("lightedArray.glsl"));
        var textureArray = FileService.Parse<TextureArray>(LocalPathService.Textures.At("block\\"));
        var material = new Material(shader, textureArray);

        var renderOptions = new RenderOptions {
            DoubleFaced = false,
            Wireframe = false,
        };

        Mesh mesh = MeshBrickGrid(grid, textureArray);
        var renderer = new MeshRenderer(mesh, material, renderOptions);

        ECSContext.EntityBuilder
            .Attach(new IdentifierComponent("Ship Entity", "bricks"), IdentifierComponent.DefaultIndex)
            .Attach(new TransformComponent(new Vector3(0, 0, 0), Quaternion.Identity), TransformComponent.DefaultIndex)
            .Attach(new MeshRendererComponent(renderer), MeshRendererComponent.DefaultIndex)
            .Build();

        mesh = MeshBrickGrid(grid, textureArray, true);
        renderer = new MeshRenderer(mesh, material, renderOptions);

        ECSContext.EntityBuilder
            .Attach(new IdentifierComponent("Ship Entity Transparent", "bricks"), IdentifierComponent.DefaultIndex)
            .Attach(new TransformComponent(new Vector3(0, 0, 0), Quaternion.Identity), TransformComponent.DefaultIndex)
            .Attach(new MeshRendererComponent(renderer), MeshRendererComponent.DefaultIndex)
            .Build();
    }

    private void CreateTerrainTest()
    {
        var solid = new Brick(1)
        {
            Name = "rock"
        };

        var simplex = new SimplexPerlin();

        BrickGrid grid = new(16);
        using (Benchmark.StartNew(nameof(Demo), nameof(CreateTerrainTest), "_CreateBrickGrid"))
        {
            const int size = 64;
            for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                {
                    const float scale = 0.05f;
                    var value = simplex.GetValue(x * scale, z * scale) + 1f;
                    int depth = (int)(value * 3) + 40;
                    for (int y = 0; y < depth; y++)
                    {
                        if (y < 35 && simplex.GetValue(x * scale, y * scale, z * scale) < -0.5f)
                            continue;
                        grid.Set(x, y, z, solid);
                    }
                }
        }

        var shader = FileService.Parse<Shader>(LocalPathService.Shaders.At("lightedArray.glsl"));
        var textureArray = FileService.Parse<TextureArray>(LocalPathService.Textures.At("block\\"));
        var material = new Material(shader, textureArray);

        var renderOptions = new RenderOptions
        {
            DoubleFaced = false,
            Wireframe = false
        };
        Mesh mesh = MeshBrickGrid(grid, textureArray);
        var renderer = new MeshRenderer(mesh, material, renderOptions);

        ECSContext.EntityBuilder
            .Attach(new IdentifierComponent("Terrain", "bricks"), IdentifierComponent.DefaultIndex)
            .Attach(new TransformComponent(new Vector3(0, 0, 25), Quaternion.Identity), TransformComponent.DefaultIndex)
            .Attach(new MeshRendererComponent(renderer), MeshRendererComponent.DefaultIndex)
            .Build();
    }

    private Mesh MeshBrickGrid(BrickGrid grid, TextureArray textureArray, bool transparent = false)
    {
        var empty = new Brick(0);

        var metalPanelTex = textureArray.IndexOf("metal_panel");

        var triangles = new List<uint>();
        var vertices = new List<Vector3>();
        var colors = new List<Vector4>();
        var uv = new List<Vector3>();
        var normals = new List<Vector3>();

        var cube = new Cube();
        var slope = new Slope();
        var thruster = FileService.Parse<Mesh>(LocalPathService.Models.At("thruster_rocket.obj"));
        var thrusterBlock = FileService.Parse<Mesh>(LocalPathService.Models.At("thruster_rocket_internal.obj"));

        HashSet<BrickGrid> builtGrids = new();
        using (Benchmark.StartNew(nameof(Demo), nameof(MeshBrickGrid), nameof(BuildBrickGridMesh)))
        {
            BuildBrickGridMesh(grid, new Vector3(-(int)grid.CenterOfMass.X, -(int)grid.CenterOfMass.Y, -(int)grid.CenterOfMass.Z));
        }

        void BuildBrickGridMesh(BrickGrid gridToBuild, Vector3 offset = default)
        {
            if (builtGrids.Contains(gridToBuild))
                return;
            else
                builtGrids.Add(gridToBuild);

            for (int nX = 0; nX < gridToBuild.NeighborGrids.GetLength(0); nX++)
                for (int nY = 0; nY < gridToBuild.NeighborGrids.GetLength(1); nY++)
                    for (int nZ = 0; nZ < gridToBuild.NeighborGrids.GetLength(2); nZ++)
                    {
                        var neighbor = gridToBuild.NeighborGrids[nX, nY, nZ];
                        if (neighbor != null)
                            BuildBrickGridMesh(neighbor, new Vector3(nX - 1, nY - 1, nZ - 1) * gridToBuild.DimensionSize + offset);
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
                            if (transparent && !brick.Name.Contains("window") && !brick.Name.Contains("porthole"))
                            {
                                continue;
                            }
                            else if (!transparent && (brick.Name.Contains("window") || brick.Name.Contains("porthole")))
                            {
                                continue;
                            }

                            Mesh brickMesh = MeshFromBrickID(brick.ID);
                            colors.AddRange(brickMesh.Colors);

                            Mesh MeshFromBrickID(int id)
                            {
                                return id switch
                                {
                                    2 => slope,
                                    3 => thruster,
                                    4 => thrusterBlock,
                                    _ => cube,
                                };
                            }

                            foreach (Vector3 texCoord in brickMesh.UV)
                            {
                                int textureIndex = textureArray.IndexOf(brick.Name);
                                uv.Add(new Vector3(texCoord.X, texCoord.Y, textureIndex >= 0 ? textureIndex : metalPanelTex));
                            }

                            foreach (uint tri in brickMesh.Triangles)
                                triangles.Add(tri + (uint)vertices.Count);

                            foreach (Vector3 normal in brickMesh.Normals)
                                normals.Add(brickMesh != cube ? Vector3.Transform(normal, brick.GetQuaternion()) : normal);

                            foreach (Vector3 vertex in brickMesh.Vertices)
                                vertices.Add((brickMesh != cube ? Vector3.Transform(vertex, brick.GetQuaternion()) : vertex) + new Vector3(x, y, z) + offset);
                        }
                    }
        }

        var mesh = new Mesh(triangles.ToArray(), vertices.ToArray(), colors.ToArray(), uv.ToArray(), normals.ToArray());
        return mesh;
    }

    private void CreateDonutDemo()
    {
        var mesh = FileService.Parse<Mesh>(LocalPathService.Models.At("donut.obj"));
        var shader = FileService.Parse<Shader>(LocalPathService.Shaders.At("lighted.glsl"));
        var texture = FileService.Parse<Texture>(LocalPathService.Textures.At("test.png"));

        var material = new Material(shader, texture);

        var renderOptions = new RenderOptions
        {
            DoubleFaced = false
        };

        ECSContext.EntityBuilder
            .Attach(new IdentifierComponent("Donut", null), IdentifierComponent.DefaultIndex)
            .Attach(new TransformComponent(new Vector3(0f, 10f, 0f), Quaternion.Identity, new Vector3(10f)), TransformComponent.DefaultIndex)
            .Attach(new MeshRendererComponent(new MeshRenderer(mesh, material, renderOptions)), MeshRendererComponent.DefaultIndex)
            .Build();
    }

    private void CreateTestEntities()
    {
        var shader = FileService.Parse<Shader>(LocalPathService.Shaders.At("textured.glsl"));

        var astronautTexture = FileService.Parse<Texture>(LocalPathService.Textures.At("astronaut.png"));
        var chortTexture = FileService.Parse<Texture>(LocalPathService.Textures.At("chort.png"));
        var hubertTexture = FileService.Parse<Texture>(LocalPathService.Textures.At("hubert.png"));
        var haroldTexture = FileService.Parse<Texture>(LocalPathService.Textures.At("harold.png"));
        var melvinTexture = FileService.Parse<Texture>(LocalPathService.Textures.At("melvin.png"));
        var womanTexture = FileService.Parse<Texture>(LocalPathService.Textures.At("woman.png"));

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
                        .Attach(new TransformComponent(new Vector3(x - 10, y - 7, z + 30), Quaternion.Identity), TransformComponent.DefaultIndex)
                        .Attach(new MeshRendererComponent(new MeshRenderer(mesh, material, renderOptions)), MeshRendererComponent.DefaultIndex)
                        .Build();
                }
            }
        }
    }

    private void CreatePhysicsTest()
    {
        var mesh = new Cube();
        var renderOptions = new RenderOptions();
        var shader = FileService.Parse<Shader>(LocalPathService.Shaders.At("textured.glsl"));

        var floorTexture = FileService.Parse<Texture>(LocalPathService.Textures.At("test.png"));
        var floorMaterial = new Material(shader, floorTexture);

        var cubeTexture = FileService.Parse<Texture>(LocalPathService.Textures.At("block").At("metal_panel.png"));
        var cubeMaterial = new Material(shader, cubeTexture);

        ECSContext.EntityBuilder
            .Attach(new IdentifierComponent("Floor", null), IdentifierComponent.DefaultIndex)
            .Attach(new TransformComponent(Vector3.Zero, Quaternion.Identity, new Vector3(16, 0, 16)), TransformComponent.DefaultIndex)
            .Attach(new MeshRendererComponent(new MeshRenderer(mesh, floorMaterial, renderOptions)), MeshRendererComponent.DefaultIndex)
            .Attach(new PhysicsComponent(Layers.NonMoving, BodyType.Static), PhysicsComponent.DefaultIndex)
            .Build();

        for (int i = 0; i < 40; i++)
        {
            ECSContext.EntityBuilder
                .Attach(new IdentifierComponent($"Phyics Body {i}", null), IdentifierComponent.DefaultIndex)
                .Attach(new TransformComponent(new Vector3(0, 20 + i * 2, 0), Quaternion.Identity), TransformComponent.DefaultIndex)
                .Attach(new MeshRendererComponent(new MeshRenderer(mesh, cubeMaterial, renderOptions)), MeshRendererComponent.DefaultIndex)
                .Attach(new PhysicsComponent(Layers.Moving, BodyType.Dynamic), PhysicsComponent.DefaultIndex)
                .Build();
        }
    }

    public override void Unload()
    {
        Debugger.Log("Dumping the log.");
        Debugger.Dump();
    }
}