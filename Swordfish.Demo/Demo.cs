using ImGuiNET;

using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

using Swordfish.Engine;
using Swordfish.Engine.ECS;
using Swordfish.Engine.Rendering;
using Swordfish.Engine.Rendering.Shapes;
using Swordfish.Engine.Rendering.UI;
using Swordfish.Engine.Extensions;
using Swordfish.Engine.Voxels;
using Swordfish.Integrations;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Types;

using Vector2 = System.Numerics.Vector2;

public class Demo
{
    static void Main(string[] args)
    {
        Demo game = new Demo();

        Swordfish.Engine.Swordfish.StartCallback = game.Start;
        Swordfish.Engine.Swordfish.UpdateCallback = game.Update;
        Swordfish.Engine.Swordfish.GuiCallback = game.ShowGui;

        Swordfish.Engine.Swordfish.Initialize();
    }

    public float cameraSpeed = 12f;
    public float cameraSpeedFast = 80f;
    public float cameraSpeedSlow = 8f;
    public bool raining = false;
    public bool showControls = true;
    public Mesh bulletMesh;

    public void CreateEntityParented(Vector3 pos, Quaternion rot)
    {
        Entity parent = Swordfish.Engine.Swordfish.ECS.CreateEntity("parentedEntity", "",
                new RenderComponent() { mesh = null },
                new RigidbodyComponent() { mass = 10, restitution = 1f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                new CollisionComponent() { size = 0.5f },
                new TransformComponent() { position = pos, orientation = rot },
                new TurntableComponent()
            );

        Swordfish.Engine.Swordfish.ECS.CreateEntity("childEntity", "",
                new RenderComponent() { mesh = null },
                new RigidbodyComponent() { mass = 10, restitution = 1f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                new CollisionComponent() { size = 0.5f },
                new TransformComponent() { parent = parent, localPosition = new Vector3(0, 1, 0), orientation = rot }
            );

        Swordfish.Engine.Swordfish.ECS.CreateEntity("childEntity", "",
                new RenderComponent() { mesh = null },
                new RigidbodyComponent() { mass = 10, restitution = 1f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                new CollisionComponent() { size = 0.5f },
                new TransformComponent() { parent = parent, localPosition = new Vector3(0, -1, 0), orientation = rot }
            );
    }

    public void CreateEntityCube(Vector3 pos, Quaternion rot)
    {
        Swordfish.Engine.Swordfish.ECS.CreateEntity("floatingCube", "",
                new RenderComponent() { mesh = null },
                new RigidbodyComponent() { mass = Swordfish.Engine.Swordfish.Random.Next(2, 10), restitution = 1f, drag = 3f, resistance = 0f, velocity = Vector3.Zero },
                new CollisionComponent() { size = 0.5f },
                new TransformComponent() { position = pos, orientation = rot },
                new TurntableComponent()
            );
    }

    public void CreateEntityCubes(int count)
    {
        for (int i = 0; i < count; i++)
        {
            CreateEntityCube(
                new Vector3(
                    Swordfish.Engine.Swordfish.Random.Next(-100, 100),
                    50,
                    Swordfish.Engine.Swordfish.Random.Next(-100, 100)),

                Quaternion.FromEulerAngles(
                    Swordfish.Engine.Swordfish.Random.Next(360),
                    Swordfish.Engine.Swordfish.Random.Next(360),
                    Swordfish.Engine.Swordfish.Random.Next(360))
                );
        }
    }

    public void Shoot()
    {
        Swordfish.Engine.Swordfish.ECS.CreateEntity("projectile", "",
                new RenderComponent() { mesh = bulletMesh },
                new RigidbodyComponent() { velocity = Camera.Main.transform.forward * 80, mass = 1f, restitution = 1f, drag = 3f, resistance = 0f },
                new CollisionComponent() { size = 0.5f },
                new TransformComponent() { position = Camera.Main.transform.position + (Camera.Main.transform.forward * 2), orientation = Quaternion.Identity }
            );
    }

    public void CreateBillboardEntity(Shader shader, Texture texture, Vector3 position, Vector3 scale)
    {
        if (Swordfish.Engine.Swordfish.ECS.CreateEntity(out Entity entity))
        {
            Mesh mesh = new Quad();
            mesh.Scale = scale;

            mesh.Material = new Material()
            {
                Name = shader.Name,
                Shader = shader,
                DiffuseTexture = texture,
                Roughness = 1f,
                Metallic = 0f
            };

            Swordfish.Engine.Swordfish.ECS.Attach(entity,
                    new RenderComponent() { mesh = mesh },
                    new RigidbodyComponent() { mass = 1f, restitution = 0f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                    new CollisionComponent() { size = 0.5f },
                    new TransformComponent() { position = position, orientation = Quaternion.Identity },
                    new BillboardComponent()
                );
        }
    }

    public void CreatePointLightEntity(Vector3 position, Vector4 color, float lumens)
    {
        if (Swordfish.Engine.Swordfish.ECS.CreateEntity(out Entity entity))
        {
            Mesh mesh = new Quad();

            mesh.Material = new Material()
            {
                Name = Shaders.UNLIT.Get().Name,
                Tint = color * lumens / 100,
                Shader = Shaders.UNLIT.Get(),
                DiffuseTexture = Icons.LIGHT.Get()
            };

            Swordfish.Engine.Swordfish.ECS.Attach(entity,
                    new RenderComponent() { mesh = mesh },
                    new TransformComponent() { position = position, orientation = Quaternion.Identity },
                    new LightComponent() { color = color, lumens = lumens },
                    new BillboardComponent()
                );
        }
    }

    public void CreateAnimatedBoardEntity(Shader shader, Texture texture, float speed, Vector3 position, Vector3 scale)
    {
        if (Swordfish.Engine.Swordfish.ECS.CreateEntity(out Entity entity))
        {
            int frames = (int)(texture.GetSize().Y / texture.GetSize().X);

            Mesh mesh = new Quad();
            mesh.Scale = scale;

            mesh.uv = new Vector3[] {
                new Vector3(0f, 1f/ frames, 0),
                new Vector3(1f, 1f/ frames, 0),
                new Vector3(1f, 0f, 0),
                new Vector3(0f, 0f, 0),
            };

            mesh.Material = new Material()
            {
                Name = shader.Name,
                Shader = shader,
                DiffuseTexture = texture,
                Roughness = 1f,
                Metallic = 0f
            };

            Swordfish.Engine.Swordfish.ECS.Attach(entity,
                    new RenderComponent() { mesh = mesh },
                    new TextureAnimationComponent() { frames = frames, speed = speed },
                    new RigidbodyComponent() { mass = 1f, restitution = 0f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                    new CollisionComponent() { size = 0.5f },
                    new TransformComponent() { position = position, orientation = Quaternion.Identity },
                    new BillboardComponent()
                );
        }
    }

    private void Start()
    {
        Debug.Enabled = true;

        CreatePointLightEntity(new Vector3(1f, 0f, 5f), Color.White, 800);

        Shader shader = Shaders.PBR.Get();

        Mesh model = OBJ.LoadFromFile($"{Directories.MODELS}/westchester.obj", "westchester");
        Texture2D tex = Texture2D.LoadFromFile($"{Directories.TEXTURES}/westchester.png", "westchester");
        model.Material = new Material()
        {
            Name = shader.Name,
            Shader = shader,
            DiffuseTexture = tex
        };

        Swordfish.Engine.Swordfish.ECS.CreateEntity("westchester", "",
                new RenderComponent() { mesh = model },
                new RigidbodyComponent() { mass = 1f, restitution = 0f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                new CollisionComponent() { size = 0.5f },
                new TransformComponent() { position = Vector3.Zero, orientation = Quaternion.Identity },
                new TurntableComponent()
            );

        model = OBJ.LoadFromFile($"{Directories.MODELS}/character.obj", "character");
        tex = Texture2D.LoadFromFile($"{Directories.TEXTURES}/character_clothes.png", "character");
        model.Material = new Material()
        {
            Name = shader.Name,
            Shader = shader,
            DiffuseTexture = tex,
            Roughness = 1f,
            Metallic = 0f
        };

        Swordfish.Engine.Swordfish.ECS.CreateEntity("character", "",
                new RenderComponent() { mesh = model },
                new RigidbodyComponent() { mass = 1f, restitution = 0f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                new CollisionComponent() { size = 0.5f },
                new TransformComponent() { position = new Vector3(0f, 0f, -5f), orientation = Quaternion.Identity },
                new TurntableComponent()
            );

        tex = Texture2D.LoadFromFile($"{Directories.TEXTURES}/astronaut.png", "astronaut");
        CreateBillboardEntity(shader, tex, new Vector3(4f, 0f, -4f), Vector3.One * 2.5f);

        tex = Texture2D.LoadFromFile($"{Directories.TEXTURES}/chort.png", "chort");
        CreateBillboardEntity(shader, tex, new Vector3(6f, 0f, -4f), Vector3.One * 2.5f);

        tex = Texture2D.LoadFromFile($"{Directories.TEXTURES}/harold.png", "harold");
        CreateBillboardEntity(shader, tex, new Vector3(8f, 0f, -4f), Vector3.One * 2.5f);

        tex = Texture2D.LoadFromFile($"{Directories.TEXTURES}/hubert.png", "hubert");
        CreateBillboardEntity(shader, tex, new Vector3(10f, 0f, -4f), Vector3.One * 2.5f);

        tex = Texture2D.LoadFromFile($"{Directories.TEXTURES}/melvin.png", "melvin");
        CreateBillboardEntity(shader, tex, new Vector3(12f, 0f, -4f), Vector3.One * 2.5f);

        tex = Texture2D.LoadFromFile($"{Directories.TEXTURES}/woman.png", "woman");
        CreateBillboardEntity(shader, tex, new Vector3(14f, 0f, -4f), Vector3.One * 2.5f);

        tex = Texture2D.LoadFromFile($"{Directories.ICONS}/controls/esc_a.png", "esc_a");
        CreateAnimatedBoardEntity(Shaders.UNLIT.Get(), tex, 0.5f, new Vector3(-4f, 0f, -4f), Vector3.One);

        tex = Texture2D.LoadFromFile($"{Directories.TEXTURES}/explosion.png", "explosion");
        CreateAnimatedBoardEntity(Shaders.UNLIT.Get(), tex, 0.75f, new Vector3(-10f, 0f, -4f), Vector3.One * 10f);

        CreateEntityParented(new Vector3(-15f, 0f, -4f), Quaternion.Identity);

        model = OBJ.LoadFromFile($"{Directories.MODELS}/donut.obj", "donut");
        tex = Texture2D.LoadFromFile($"{Directories.TEXTURES}/test.png", "donut");
        model.Material = new Material()
        {
            Name = shader.Name,
            Shader = shader,
            DiffuseTexture = tex,
        };

        bulletMesh = model;

        Swordfish.Engine.Swordfish.ECS.CreateEntity("donut", "",
                new RenderComponent() { mesh = model },
                new RigidbodyComponent() { mass = 1f, restitution = 0f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                new CollisionComponent() { size = 0.5f },
                new TransformComponent() { position = new Vector3(4f, 0f, -2f), orientation = Quaternion.Identity },
                new TurntableComponent()
            );

        VoxelObject voxels = new VoxelObject();
        voxels.BuildMesh();

        model = voxels.Mesh;
        tex = Texture2D.LoadFromFile($"{Directories.TEXTURES}/test.png", "voxels");
        model.Material = new Material()
        {
            Name = shader.Name,
            Shader = shader,
            DiffuseTexture = tex,
        };

        Swordfish.Engine.Swordfish.ECS.CreateEntity("voxelObject", "",
                new RenderComponent() { mesh = model },
                new RigidbodyComponent() { mass = 1f, restitution = 0f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                new CollisionComponent() { size = 0.5f },
                new TransformComponent() { position = new Vector3(10f, 0f, 0f), orientation = Quaternion.Identity }
            );
    }

    private void ShowGui()
    {
        if (showControls)
        {
            ImGui.SetNextWindowPos(new Vector2(Swordfish.Engine.Swordfish.Settings.Window.WIDTH - 700, 0));
            ImGui.SetNextWindowSizeConstraints(new Vector2(700, 300), new Vector2(700, 600));
            ImGui.Begin("Controls", WindowFlagPresets.FLAT | ImGuiWindowFlags.NoBringToFrontOnFocus);
                ImGui.Columns(3);
                ImGui.SetColumnOffset(1, 200);
                ImGui.SetColumnOffset(2, 500);

                ImGui.Image(Keys.GraveAccent.GetIcon().GetIntPtr(), Keys.GraveAccent.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"{FontAwesome.Terminal} Console");

                ImGui.Image(Keys.F1.GetIcon().GetIntPtr(), Keys.F1.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"{FontAwesome.Keyboard} Controls");

                ImGui.Image(Keys.F2.GetIcon().GetIntPtr(), Keys.F2.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"{FontAwesome.Bug} Debug");

                ImGui.Image(Keys.F3.GetIcon().GetIntPtr(), Keys.F3.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"{FontAwesome.DiagramProject} Stats");

                ImGui.Image(Keys.F4.GetIcon().GetIntPtr(), Keys.F4.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"{FontAwesome.ChartBar} Profiler");

                ImGui.NextColumn();

                ImGui.Image(Keys.W.GetIcon().GetIntPtr(), Keys.W.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Image(Keys.A.GetIcon().GetIntPtr(), Keys.A.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Image(Keys.S.GetIcon().GetIntPtr(), Keys.S.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Image(Keys.D.GetIcon().GetIntPtr(), Keys.D.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"Movement");

                ImGui.Image(Keys.Space.GetIcon().GetIntPtr(), Keys.Space.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Image(Keys.LeftControl.GetIcon().GetIntPtr(), Keys.LeftControl.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"Up/Down");

                ImGui.Image(Keys.Q.GetIcon().GetIntPtr(), Keys.Q.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Image(Keys.E.GetIcon().GetIntPtr(), Keys.E.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"Roll");

                ImGui.Image(Keys.Tab.GetIcon().GetIntPtr(), Keys.Tab.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"Mouselook");

                ImGui.Image(Keys.LeftShift.GetIcon().GetIntPtr(), Keys.LeftShift.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Image(Keys.LeftAlt.GetIcon().GetIntPtr(), Keys.LeftAlt.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"Speed up/down");

                ImGui.NextColumn();

                ImGui.Image(Keys.Equal.GetIcon().GetIntPtr(), Keys.Equal.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Image(Keys.Minus.GetIcon().GetIntPtr(), Keys.Minus.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"Timescale");

                ImGui.Image(Keys.C.GetIcon().GetIntPtr(), Keys.C.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"Zoom");

                ImGui.Image(Keys.F5.GetIcon().GetIntPtr(), Keys.F5.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"Cube Spawner");

                ImGui.Image(Keys.F6.GetIcon().GetIntPtr(), Keys.F6.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"Cube Field");

                ImGui.Image(Keys.Escape.GetIcon().GetIntPtr(), Keys.Escape.GetIcon().GetSize().ToSysVector() * 2f);
                ImGui.SameLine(); ImGui.Text($"Exit");

                ImGui.Columns();
            ImGui.End();
        }
    }

    private void Update()
    {
        //  The camera should behave independent of the engine delta, which is scaled by the timescale
        float delta = Swordfish.Engine.Swordfish.MainWindow.DeltaTime;

        if (raining)
            CreateEntityCubes(1);

        if (Input.IsKeyDown(Keys.Escape))
            Swordfish.Engine.Swordfish.Shutdown();

        if (Input.IsKeyPressed(Keys.F1))
            showControls = !showControls;

        if (Input.IsKeyPressed(Keys.GraveAccent))
            Debug.Console = !Debug.Console;

        if (Input.IsKeyPressed(Keys.F2))
            Debug.Enabled = !Debug.Enabled;

        if (Input.IsKeyPressed(Keys.F3))
            Debug.Stats = !Debug.Stats;

        if (Input.IsKeyPressed(Keys.F4))
            Debug.Profiling = !Debug.Profiling;

        if (Input.IsKeyPressed(Keys.F5))
            raining = !raining;

        if (Input.IsKeyPressed(Keys.F6))
            CreateEntityCubes(1000);

        if (Input.IsMousePressed(0) && !ImGui.IsAnyItemHovered())
            Shoot();

        if (Input.IsMousePressed(1) && !ImGui.IsAnyItemHovered())
            CreateEntityCube(Camera.Main.transform.position + (Camera.Main.transform.forward * 2), Quaternion.Identity);

        if (Input.IsKeyDown(Keys.Equal))
            Swordfish.Engine.Swordfish.Timescale += 0.5f * delta;

        if (Input.IsKeyDown(Keys.Minus))
            Swordfish.Engine.Swordfish.Timescale -= 0.5f * delta;

        if (Input.IsKeyDown(Keys.W))
            Camera.Main.transform.position += Camera.Main.transform.forward * cameraSpeed * delta;
        if (Input.IsKeyDown(Keys.S))
            Camera.Main.transform.position -= Camera.Main.transform.forward * cameraSpeed * delta;

        if (Input.IsKeyDown(Keys.A))
            Camera.Main.transform.position += Camera.Main.transform.right * cameraSpeed * delta;
        if (Input.IsKeyDown(Keys.D))
            Camera.Main.transform.position -= Camera.Main.transform.right * cameraSpeed * delta;

        if (Input.IsKeyDown(Keys.Space))
            Camera.Main.transform.position += Camera.Main.transform.up * cameraSpeed * delta;
        if (Input.IsKeyDown(Keys.LeftControl))
            Camera.Main.transform.position -= Camera.Main.transform.up * cameraSpeed * delta;

        if (Input.IsKeyDown(Keys.E))
            Camera.Main.transform.Rotate(Vector3.UnitZ, 40 * delta);
        if (Input.IsKeyDown(Keys.Q))
            Camera.Main.transform.Rotate(Vector3.UnitZ, -40 * delta);

        if (Input.IsKeyPressed(Keys.C))
            Camera.Main.FOV = 15f;
        else if (Input.IsKeyReleased(Keys.C))
            Camera.Main.FOV = 70f;

        if (Input.IsKeyPressed(Keys.LeftShift))
            cameraSpeed += cameraSpeedFast;
        else if (Input.IsKeyReleased(Keys.LeftShift))
            cameraSpeed -= cameraSpeedFast;

        if (Input.IsKeyPressed(Keys.LeftAlt))
            cameraSpeed -= cameraSpeedSlow;
        else if (Input.IsKeyReleased(Keys.LeftAlt))
            cameraSpeed += cameraSpeedSlow;

        if (Input.IsKeyPressed(Keys.Tab))
        {
            Input.CursorGrabbed = !Input.CursorGrabbed;
            if (!Input.CursorGrabbed)
                Input.CursorVisible = true;
        }

        if (Input.CursorGrabbed)
        {
            Camera.Main.transform.Rotate(Vector3.UnitY, Input.MouseState.Delta.X * 0.05f);
            Camera.Main.transform.Rotate(Vector3.UnitX, Input.MouseState.Delta.Y * 0.05f);
        }
    }
}