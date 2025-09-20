using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using LibNoise.Primitive;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Constraints;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.Library.Types.Shapes;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Types;
using Swordfish.UI;
using Swordfish.UI.Elements;
using Swordfish.UI.Reef;

// ReSharper disable UnusedMember.Local

namespace Swordfish.Demo;

// ReSharper disable once ClassNeverInstantiated.Global
public class Demo : IEntryPoint, IAutoActivate
{
    private static readonly uint[] _triangles =
    [
        3, 1, 0,
        3, 2, 1,
    ];

    private static readonly Vector3[] _vertices =
    [
        new(0.5f,  0.5f, 0.0f),
        new(0.5f, -0.5f, 0.0f),
        new(-0.5f, -0.5f, 0.0f),
        new(-0.5f,  0.5f, 0.0f),
    ];

    private static readonly Vector4[] _colors =
    [
        Vector4.One,
        Vector4.One,
        Vector4.One,
        Vector4.One,
    ];

    private static readonly Vector3[] _uv =
    [
        new(1f, 0f, 0f),
        new(1f, 1f, 0f),
        new(0f, 1f, 0f),
        new(0f, 0f, 0f),
    ];

    private static readonly Vector3[] _normals =
    [
        new(1f),
        new(1f),
        new(1f),
        new(1f),
    ];

    private readonly ReefContext _reefContext;
    private readonly IECSContext _ecsContext;
    private readonly IRenderContext _renderContext;
    private readonly IWindowContext _windowContext;
    private readonly IFileParseService _fileParseService;
    private readonly IPhysics _physics;
    private readonly IInputService _inputService;
    private readonly TextElement _debugText;
    private readonly ILogger _logger;
    private readonly Material _swordfishMaterial;

    public Demo(ReefContext reefContext, IECSContext ecsContext, IRenderContext renderContext, IWindowContext windowContext, IFileParseService fileParseService, IPhysics physics, IInputService inputService, ILineRenderer lineRenderer, ILogger logger, IUIContext uiContext)
    {
        _reefContext = reefContext;
        _fileParseService = fileParseService;
        _ecsContext = ecsContext;
        _renderContext = renderContext;
        _windowContext = windowContext;
        _physics = physics;
        _inputService = inputService;
        _logger = logger;

        // _windowContext.Update += OnWindowUpdate;

        _debugText = new TextElement("");
        // ReSharper disable once UnusedVariable
        CanvasElement myCanvas = new(uiContext, windowContext, "Demo Debug Canvas")
        {
            Constraints = new RectConstraints
            {
                Anchor = ConstraintAnchor.TOP_RIGHT,
                X = new RelativeConstraint(0.2f),
                Y = new RelativeConstraint(0.1f),
                Width = new RelativeConstraint(0.1f),
                Height = new RelativeConstraint(0.1f),
            },
            Content = {
                new PanelElement("Demo Debug Panel")
                {
                    Constraints = new RectConstraints
                    {
                        Height = new FillConstraint(),
                    },
                    Tooltip = new Tooltip
                    {
                        Help = true,
                        Text = "This is a panel for debugging in the demo.",
                    },
                    Content = {
                        _debugText,
                    },
                },
            },
        };

        inputService.Scrolled += OnScroll;
        _physics.FixedUpdate += OnFixedUpdate;
        _positionGizmo = new PositionGizmo(lineRenderer, _renderContext.Camera.Get());
        _orientationGizmo = new OrientationGizmo(lineRenderer, _renderContext.Camera.Get());
        _scaleGizmo = new ScaleGizmo(lineRenderer, _renderContext.Camera.Get());
        
        var shader = _fileParseService.Parse<Shader>(AssetPaths.Shaders.At("ui_reef_textured.glsl"));
        var astronautTexture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("astronaut.png"));
        _swordfishMaterial = new Material(shader, astronautTexture);
    }

    private double _time;
    private void OnWindowUpdate(double delta)
    {
        UIBuilder<Material> ui = _reefContext.Builder;

        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
                Y = new Relative(0.5f),
                Width = new Fixed(200),
                Height = new Fixed(200),
            };

            _time += delta;
            float pingPong = (float)MathS.PingPong(_time, 1d);
            if (ui.Held("abc"))
            {
                ui.Color = new Vector4(1f, 0f, 0f, 1f);
            }
            else
            {
                ui.Color = new Vector4(pingPong, pingPong, pingPong, 1f);
            }
        }
        
        using (ui.Element())
        {
            ui.Color = new Vector4(0f, 0.15f, 0.25f, 1f);
            ui.CornerRadius = new CornerRadius(topLeft: 20, topRight: 20, bottomLeft: 20, bottomRight: 20);
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Padding = new Padding(
                left: 20,
                top: 20,
                right: 20,
                bottom: 20
            );
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Right,
                X = new Relative(0.98f),
                Y = new Relative(0.5f),
                Width = new Fixed(200),
                Height = new Fixed(600),
            };
            
            ui.ClipConstraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };

            ui.VerticalScroll = true;
            ui.ScrollY -= 214;

            for (var i = 0; i < 50; i++)
            {
                using (ui.Text($"Item {i} and some more extra text to wrap"))
                {
                    ui.FontID = "monogram";
                }
            }
        }

        using (ui.Element())
        {
            ui.CornerRadius = new CornerRadius(topLeft: 0, topRight: 50, bottomLeft: 100, bottomRight: 10);
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Spacing = 8;
            ui.Padding = new Padding(
                left: 8,
                top: 8,
                right: 8,
                bottom: 8
            );
            ui.Color = new Vector4(0f, 0f, 0f, 1f);
            ui.Constraints = new Constraints
            {
                Width = new Fixed(400),
                Height = new Fixed(300),
            };
            
            using (ui.Text("Hello world!"))
            {
                ui.FontSize = 10;
                ui.Color = new Vector4(1f, 0f, 0f, 1f);
            }
            
            using (ui.Text("\uf24e \uf2bd \uf03e \uf118 \uf164"))
            {
                ui.FontOptions = new FontOptions { ID = "Font Awesome 6 Free Solid", Size = 20 };
            }
            
            using (ui.Text("This is a little bit of a longer piece of text with a background color."))
            {
                ui.BackgroundColor = new Vector4(0f, 0f, 1f, 1f);
            }
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(64),
                    Height = new Fixed(64),
                };
            }
            
            using (ui.Element())
            {
                ui.BackgroundColor = new Vector4(0f, 1f, 0f, 1f);
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                };
            }
        }
        
        using (ui.Image(_swordfishMaterial))
        {
            ui.Padding = new Padding(8, 8, 8, 8);
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.BackgroundColor = new Vector4(0f, 0f, 0f, 1f);
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
                Y = new Relative(0.1f),
                Width = new Fixed(128),
                Height = new Fixed(128),
            };

            using (ui.Text("Swordfish"))
            {
                ui.BackgroundColor = new Vector4(1f, 0f, 0f, 1f);
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    X = new Relative(0.5f),
                    Y = new Relative(0.5f),
                };
            }
        }

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = new Vector4(1f);
            ui.Constraints = new Constraints
            {
                X = new Relative(0.25f),
                Y = new Relative(0.25f),
                Width = new Fixed(200),
                Height = new Fixed(200),
            };
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0.5f, 0f, 0f, 1f);
                ui.Constraints = new Constraints
                {
                    X = new Relative(0f),
                    Y = new Relative(0f),
                    Width = new Fixed(100),
                    Height = new Fixed(100),
                };
            }
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0f, 0.5f, 0f, 1f);
                ui.Constraints = new Constraints
                {
                    X = new Relative(0.5f),
                    Y = new Relative(0.5f),
                    Width = new Fixed(100),
                    Height = new Fixed(100),
                };
            }
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0f, 0f, 0.5f, 1f);
                ui.Constraints = new Constraints
                {
                    X = new Relative(0.5f),
                    Y = new Relative(0f),
                    Width = new Relative(0.5f),
                    Height = new Relative(0.5f),
                };
            }
        }

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Color = new Vector4(0.25f, 0.25f, 0.25f, 1f);
            ui.Spacing = 8;
            ui.Padding = new Padding(
                left: 8,
                top: 8,
                right: 8,
                bottom: 8
            );
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
                Y = new Relative(0.5f),
                Width = new Fixed(500),
            };
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0f, 0.5f, 0.5f, 1f);
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                };
            }

            if (ui.Button(id: "button1", text: "test"))
            {
                using (ui.Text("Clicked!")) { }
            }
            
            using (ui.Element())
            {
                if (ui.Clicked("click me"))
                {
                    ui.Color = new Vector4(0f, 0f, 0f, 1f);
                }
                else
                {
                    ui.Color = new Vector4(0f, 0.5f, 0.5f, 1f);
                }

                using (ui.Text("Click me"))
                {
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                        X = new Relative(0.5f),
                        Y = new Relative(0.5f),
                    };
                }

                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0f, 0.5f, 0.5f, 1f);
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                };
            }
        }
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Color = new Vector4(0.25f, 0.25f, 0.25f, 1f);
            ui.Spacing = 8;
            ui.Padding = new Padding(
                left: 8,
                top: 8,
                right: 8,
                bottom: 8
            );
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                X = new Relative(0.5f),
                Y = new Relative(0.99f),
            };
        
            for (var i = 0; i < 6; i++)
            {
                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Padding = new Padding(left: 4, top: 4, right: 4, bottom: 4);
                    ui.Color = new Vector4(0f, 0.5f, 0.5f, 1f);
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(50),
                        Height = new Fixed(50),
                    };
                    
                    using (ui.Text(i.ToString())) { }

                    using (ui.Element())
                    {
                        ui.Color = new Vector4(1f);
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Bottom | Anchors.Right,
                            X = new Relative(1f),
                            Y = new Relative(1f),
                            Width = new Fixed(20),
                            Height = new Fixed(20),
                        };
                    }
                }
            }
        }
    }

    private readonly PositionGizmo _positionGizmo;
    private readonly OrientationGizmo _orientationGizmo;
    private readonly ScaleGizmo _scaleGizmo;

    private void OnFixedUpdate(object? sender, EventArgs args)
    {
        Camera camera = _renderContext.Camera.Get();
        Vector2 cursorPos = _inputService.CursorPosition;
        Ray ray = camera.ScreenPointToRay((int)cursorPos.X, (int)cursorPos.Y, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y);
        RaycastResult raycast = _physics.Raycast(ray * 1000);

        if (!raycast.Hit)
        {
            _debugText.Text = "";
            return;
        }

        _debugText.Text = $"Hovering {raycast.Entity.Get<IdentifierComponent>()?.Name} ({raycast.Entity.Ptr})";

        if (!_inputService.IsMouseHeld(MouseButton.Left))
        {
            return;
        }

        _ecsContext.World.DataStore.Query<TransformComponent>(raycast.Entity.Ptr, 1f, UpdateTransform);
        void UpdateTransform(float delta, DataStore store, int entity, ref TransformComponent transform)
        {
            float scroll = _scrollBuffer;
            transform.Position = raycast.Point;
            if (scroll != 0)
            {
                transform.Orientation = Quaternion.Multiply(transform.Orientation, Quaternion.CreateFromYawPitchRoll(15 * scroll * MathS.DEGREES_TO_RADIANS, 0, 0));
                Interlocked.Exchange(ref _scrollBuffer, _scrollBuffer - scroll);
            }

            _positionGizmo.Render(transform);
            _orientationGizmo.Render(transform);
            _scaleGizmo.Render(transform);
        }

        _ecsContext.World.DataStore.Query<PhysicsComponent>(raycast.Entity.Ptr, 1f, UpdatePhysics);
        void UpdatePhysics(float delta, DataStore store, int entity, ref PhysicsComponent physics)
        {
            physics.Velocity = Vector3.Zero;
            physics.Torque = Vector3.Zero;
        }
    }

    private volatile float _scrollBuffer;
    private void OnScroll(object? sender, ScrolledEventArgs e)
    {
        Camera camera = _renderContext.Camera.Get();
        Vector2 cursorPos = _inputService.CursorPosition;
        Ray ray = camera.ScreenPointToRay((int)cursorPos.X, (int)cursorPos.Y, (int)_windowContext.Resolution.X, (int)_windowContext.Resolution.Y);
        RaycastResult raycast = _physics.Raycast(ray * 1000);

        if (!raycast.Hit)
        {
            return;
        }

        if (!_inputService.IsMouseHeld(MouseButton.Left))
        {
            return;
        }

        TransformComponent? transform = raycast.Entity.Get<TransformComponent>();
        if (transform == null)
        {
            return;
        }

        Interlocked.Exchange(ref _scrollBuffer, _scrollBuffer + e.Delta);
    }

    public void Run()
    {
        // TestUI.CreateCanvas();
        // TestECS.Populate(ECSContext);
        // CreateTestEntities();
        // CreateStressTest();
        // CreateShipTest();
        // CreateDonutDemo();
        CreatePhysicsTest();

        _renderContext.Camera.Get().Transform.Position = new Vector3(0, 5, 15);
        _renderContext.Camera.Get().Transform.Rotate(new Vector3(-30, 0, 0));

        foreach (string line in Benchmark.CollectOutput())
        {
            _logger.LogInformation("[Benchmark] {line}", line);
        }
    }

    private void CreateStressTest()
    {
        var empty = new Brick(0);
        var solid = new Brick(1);

        BrickGrid grid = new(16);
        using (Benchmark.StartNew(nameof(Demo), nameof(CreateStressTest), "_CreateBrickGrid"))
        {
            const int size = 200;
            for (var x = 0; x < size; x++)
            for (var y = 0; y < size; y++)
            for (var z = 0; z < size; z++)
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
            for (var nX = 0; nX < gridToBuild.NeighborGrids.GetLength(0); nX++)
            for (var nY = 0; nY < gridToBuild.NeighborGrids.GetLength(1); nY++)
            for (var nZ = 0; nZ < gridToBuild.NeighborGrids.GetLength(2); nZ++)
            {
                BuildBrickGridMesh(gridToBuild.NeighborGrids[nX, nY, nZ], new Vector3(nX - 1, nY - 1, nZ - 1) * gridToBuild.DimensionSize + offset);
            }

            for (var x = 0; x < gridToBuild.DimensionSize; x++)
            for (var y = 0; y < gridToBuild.DimensionSize; y++)
            for (var z = 0; z < gridToBuild.DimensionSize; z++)
            {
                Brick brick = gridToBuild.Bricks[x, y, z];
                Quaternion quaternion = brick.GetQuaternion();
                if (brick.Equals(empty) ||
                    (!gridToBuild.Get(x + 1, y, z).Equals(empty)
                     && !gridToBuild.Get(x - 1, y, z).Equals(empty)
                     && !gridToBuild.Get(x, y + 1, z).Equals(empty)
                     && !gridToBuild.Get(x, y - 1, z).Equals(empty)
                     && !gridToBuild.Get(x, y, z + 1).Equals(empty)
                     && !gridToBuild.Get(x, y, z - 1).Equals(empty)))
                {
                    continue;
                }

                Mesh brickMesh = brick.ID == 1 ? cube : slope;
                colors.AddRange(brickMesh.Colors);
                normals.AddRange(brickMesh.Normals);
                uv.AddRange(brickMesh.Uv);

                foreach (uint tri in brickMesh.Triangles)
                {
                    triangles.Add(tri + (uint)vertices.Count);
                }

                foreach (Vector3 vertex in brickMesh.Vertices)
                {
                    vertices.Add(Vector3.Transform(vertex - new Vector3(0.5f), quaternion) + new Vector3(0.5f) + new Vector3(x, y, z) + offset);
                }
            }
        }

        var mesh = new Mesh(triangles.ToArray(), vertices.ToArray(), colors.ToArray(), uv.ToArray(), normals.ToArray());

        var shader = _fileParseService.Parse<Shader>(AssetPaths.Shaders.At("lighted.glsl"));
        var texture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("block/metal_panel.png"));
        var material = new Material(shader, texture);

        var renderOptions = new RenderOptions {
            DoubleFaced = false,
        };

        var renderer = new MeshRenderer(mesh, material, renderOptions);

        DataStore store = _ecsContext.World.DataStore;
        int entity = store.Alloc(new IdentifierComponent("Brick Entity", "bricks"));
        store.AddOrUpdate(entity, new TransformComponent(new Vector3(0, 0, 300), Quaternion.Identity));
        store.AddOrUpdate(entity, new MeshRendererComponent(renderer));
    }

    private void CreateShipTest()
    {
        var empty = new Brick(0);

        BrickGrid grid;
        using (Benchmark.StartNew(nameof(Demo), nameof(CreateShipTest), "_LoadBrickGrid"))
        {
            grid = _fileParseService.Parse<BrickGrid>(AssetPaths.Root.At("saves").At("mainMenuVoxObj.svo"));
        }

        var triangles = new List<uint>();
        var vertices = new List<Vector3>();
        var colors = new List<Vector4>();
        var uv = new List<Vector3>();
        var normals = new List<Vector3>();

        var cube = new Cube();
        var slope = new Slope();
        var thruster = _fileParseService.Parse<Mesh>(AssetPaths.Meshes.At("thruster_rocket.obj"));
        var thrusterBlock = _fileParseService.Parse<Mesh>(AssetPaths.Meshes.At("thruster_rocket_internal.obj"));
        using (Benchmark.StartNew(nameof(Demo), nameof(CreateShipTest), "_BuildBrickGridMesh"))
        {
            BuildBrickGridMesh(grid, -grid.CenterOfMass);
        }

        void BuildBrickGridMesh(BrickGrid gridToBuild, Vector3 offset = default)
        {
            for (var nX = 0; nX < gridToBuild.NeighborGrids.GetLength(0); nX++)
            for (var nY = 0; nY < gridToBuild.NeighborGrids.GetLength(1); nY++)
            for (var nZ = 0; nZ < gridToBuild.NeighborGrids.GetLength(2); nZ++)
            {
                BuildBrickGridMesh(gridToBuild.NeighborGrids[nX, nY, nZ], new Vector3(nX - 1, nY - 1, nZ - 1) * gridToBuild.DimensionSize + offset);
            }

            for (var x = 0; x < gridToBuild.DimensionSize; x++)
            for (var y = 0; y < gridToBuild.DimensionSize; y++)
            for (var z = 0; z < gridToBuild.DimensionSize; z++)
            {
                Brick brick = gridToBuild.Bricks[x, y, z];
                if (brick.Equals(empty) ||
                    (!gridToBuild.Get(x + 1, y, z).Equals(empty)
                     && !gridToBuild.Get(x - 1, y, z).Equals(empty)
                     && !gridToBuild.Get(x, y + 1, z).Equals(empty)
                     && !gridToBuild.Get(x, y - 1, z).Equals(empty)
                     && !gridToBuild.Get(x, y, z + 1).Equals(empty)
                     && !gridToBuild.Get(x, y, z - 1).Equals(empty)))
                {
                    continue;
                }

                Mesh brickMesh = MeshFromBrickID(brick.ID);
                colors.AddRange(brickMesh.Colors);
                uv.AddRange(brickMesh.Uv);

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
                {
                    triangles.Add(tri + (uint)vertices.Count);
                }

                foreach (Vector3 normal in brickMesh.Normals)
                {
                    normals.Add(Vector3.Transform(normal, brick.GetQuaternion()));
                }

                foreach (Vector3 vertex in brickMesh.Vertices)
                {
                    vertices.Add(Vector3.Transform(vertex, brick.GetQuaternion()) + new Vector3(x, y, z) + offset);
                }
            }
        }

        var mesh = new Mesh(triangles.ToArray(), vertices.ToArray(), colors.ToArray(), uv.ToArray(), normals.ToArray());

        var shader = _fileParseService.Parse<Shader>(AssetPaths.Shaders.At("lighted.glsl"));
        var texture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("block/metal_panel.png"));
        var material = new Material(shader, texture);

        var renderOptions = new RenderOptions {
            DoubleFaced = false,
            Wireframe = false,
        };

        var renderer = new MeshRenderer(mesh, material, renderOptions);

        DataStore store = _ecsContext.World.DataStore;
        int entity = store.Alloc(new IdentifierComponent("Ship Entity", "bricks"));
        store.AddOrUpdate(entity, new TransformComponent(new Vector3(0, 0, 20), Quaternion.Identity));
        store.AddOrUpdate(entity, new MeshRendererComponent(renderer));
    }

    private void CreateDonutDemo()
    {
        var mesh = _fileParseService.Parse<Mesh>(AssetPaths.Meshes.At("donut.obj"));
        var shader = _fileParseService.Parse<Shader>(AssetPaths.Shaders.At("lighted.glsl"));
        var texture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("test.png"));

        var material = new Material(shader, texture);

        var renderOptions = new RenderOptions
        {
            DoubleFaced = false,
        };

        DataStore store = _ecsContext.World.DataStore;
        int entity = store.Alloc(new IdentifierComponent("Donut", null));
        store.AddOrUpdate(entity, new TransformComponent(new Vector3(0f, 10f, 0f), Quaternion.Identity, new Vector3(10f)));
        store.AddOrUpdate(entity, new MeshRendererComponent(new MeshRenderer(mesh, material, renderOptions)));
    }

    private void CreateTestEntities()
    {
        var shader = _fileParseService.Parse<Shader>(AssetPaths.Shaders.At("textured.glsl"));

        var astronautTexture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("astronaut.png"));
        var chortTexture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("chort.png"));
        var hubertTexture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("hubert.png"));
        var haroldTexture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("harold.png"));
        var melvinTexture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("melvin.png"));
        var womanTexture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("woman.png"));

        var astronautMaterial = new Material(shader, astronautTexture);
        var chortMaterial = new Material(shader, chortTexture);
        var hubertMaterial = new Material(shader, hubertTexture);
        var haroldMaterial = new Material(shader, haroldTexture);
        var melvinMaterial = new Material(shader, melvinTexture);
        var womanMaterial = new Material(shader, womanTexture);

        var mesh = new Mesh(_triangles, _vertices, _colors, _uv, _normals);

        var renderOptions = new RenderOptions
        {
            DoubleFaced = true,
        };

        var random = new Randomizer();

        var index = 0;
        for (var x = 0; x < 20; x++)
        {
            for (var y = 0; y < 20; y++)
            {
                for (var z = 0; z < 30; z++)
                {
                    Material? material = random.Select(astronautMaterial, chortMaterial, hubertMaterial, haroldMaterial, melvinMaterial, womanMaterial);

                    DataStore store = _ecsContext.World.DataStore;
                    int entity = store.Alloc(new IdentifierComponent($"{material.Textures[0].Name}{index++}", null));
                    store.AddOrUpdate(entity, new TransformComponent(new Vector3(x - 10, y - 7, z + 30), Quaternion.Identity));
                    store.AddOrUpdate(entity, new MeshRendererComponent(new MeshRenderer(mesh, material, renderOptions)));
                }
            }
        }
    }

    private void CreatePhysicsTest()
    {
        var mesh = new Cube();
        var renderOptions = new RenderOptions();
        var shader = _fileParseService.Parse<Shader>(AssetPaths.Shaders.At("textured.glsl"));

        var floorTexture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("test.png"));
        var floorMaterial = new Material(shader, floorTexture);

        var cubeTexture = _fileParseService.Parse<Texture>(AssetPaths.Textures.At("block").At("metal_panel.png"));
        var cubeMaterial = new Material(shader, cubeTexture);

        Entity entity = _ecsContext.World.NewEntity();
        entity.AddOrUpdate(new IdentifierComponent("Floor", null));
        entity.AddOrUpdate(new TransformComponent(Vector3.Zero, Quaternion.Identity, new Vector3(160, 0, 160)));
        entity.AddOrUpdate(new MeshRendererComponent(new MeshRenderer(mesh, floorMaterial, renderOptions)));
        entity.AddOrUpdate(new PhysicsComponent(Layers.NON_MOVING, BodyType.Static, CollisionDetection.Discrete));
        entity.AddOrUpdate(new ColliderComponent(new Box3(Vector3.One)));

        for (var i = 0; i < 1000; i++)
        {
            entity = _ecsContext.World.NewEntity();
            entity.AddOrUpdate(new IdentifierComponent($"Physics Body {i}", null));
            entity.AddOrUpdate(new TransformComponent(new Vector3(0, 200 + i * 2, 0), Quaternion.Identity));
            entity.AddOrUpdate(new MeshRendererComponent(new MeshRenderer(mesh, cubeMaterial, renderOptions)));
            entity.AddOrUpdate(new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Discrete));
            entity.AddOrUpdate(new ColliderComponent(new Box3(Vector3.One)));
        }
    }
}