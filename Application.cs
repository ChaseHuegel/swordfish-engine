using ImGuiNET;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

using Swordfish;
using Swordfish.Diagnostics;
using Swordfish.ECS;
using Swordfish.Rendering;
using Swordfish.Rendering.Shapes;
using Swordfish.Rendering.UI;
using Swordfish.Types;
using Vector2 = System.Numerics.Vector2;

namespace source
{
    public class Application
    {
        static void Main(string[] args)
        {
            Application game = new Application();

            Engine.StartCallback = game.Start;
            Engine.UpdateCallback = game.Update;
            Engine.GuiCallback = game.ShowGui;

            Engine.Initialize();
        }

        public float cameraSpeed = 12f;
        public bool raining = false;
        public bool showControls = true;

        public void CreateEntityCube(Vector3 pos, Quaternion rot)
        {
            Engine.ECS.CreateEntity("floatingCube", "",
                    new RenderComponent() { mesh = null },
                    new RigidbodyComponent() { mass = Engine.Random.Next(2, 10), restitution = 0f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                    new CollisionComponent() { size = 0.5f },
                    new PositionComponent() { position = pos },
                    new RotationComponent() { orientation = rot },
                    new TurntableComponent()
                );
        }

        public void CreateEntityCubes(int count)
        {
            for (int i = 0; i < count; i++)
            {
                CreateEntityCube(
                    new Vector3(
                        Engine.Random.Next(-100, 100),
                        50,
                        Engine.Random.Next(-100, 100)),

                    Quaternion.FromEulerAngles(
                        Engine.Random.Next(360),
                        Engine.Random.Next(360),
                        Engine.Random.Next(360))
                    );
            }
        }

        public void Shoot()
        {
            Engine.ECS.CreateEntity("projectileCube", "",
                    new RenderComponent() { mesh = null },
                    new RigidbodyComponent() { velocity = Camera.Main.transform.forward * 80, mass = Engine.Random.Next(2, 10), restitution = 1f, drag = 3f, resistance = 0f },
                    new CollisionComponent() { size = 0.5f },
                    new PositionComponent() { position = Camera.Main.transform.position + (Camera.Main.transform.forward * 2) },
                    new RotationComponent() { orientation = Quaternion.Identity }
                );
        }

        public void CreateBillboardEntity(Shader shader, Texture texture, Vector3 position, Vector3 scale)
        {
            if (Engine.ECS.CreateEntity(out Entity entity))
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

                mesh.Bind();

                Engine.ECS.Attach(entity,
                        new RenderComponent() { mesh = mesh },
                        new RigidbodyComponent() { mass = 1f, restitution = 0f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                        new CollisionComponent() { size = 0.5f },
                        new PositionComponent() { position = position },
                        new RotationComponent() { orientation = Quaternion.Identity },
                        new BillboardComponent()
                    );
            }
        }

        public void CreatePointLightEntity(Vector3 position, Vector4 color, float lumens)
        {
            if (Engine.ECS.CreateEntity(out Entity entity))
            {
                Mesh mesh = new Quad();

                mesh.Material = new Material()
                {
                    Name = Shaders.UNLIT.Get().Name,
                    Tint = color * lumens / 100,
                    Shader = Shaders.UNLIT.Get(),
                    DiffuseTexture = Icons.LIGHT.Get()
                };

                mesh.Bind();

                Engine.ECS.Attach(entity,
                        new RenderComponent() { mesh = mesh },
                        new PositionComponent() { position = position },
                        new RotationComponent() { orientation = Quaternion.Identity },
                        new LightComponent() { color = color, lumens = lumens },
                        new BillboardComponent()
                    );
            }
        }

        private void Start()
        {
            Debug.Enabled = true;

            CreatePointLightEntity(new Vector3(1f, 0f, 5f), Color.White, 800);

            Shader shader = Shaders.PBR.Get();

            Mesh model = OBJ.LoadFromFile("resources/models/westchester.obj", "westchester");
            Texture2D tex = Texture2D.LoadFromFile("resources/textures/westchester.png", "westchester");
            model.Material = new Material()
            {
                Name = shader.Name,
                Shader = shader,
                DiffuseTexture = tex
            };
            model.Bind();

            Engine.ECS.CreateEntity("westchester", "",
                    new RenderComponent() { mesh = model },
                    new RigidbodyComponent() { mass = 1f, restitution = 0f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                    new CollisionComponent() { size = 0.5f },
                    new PositionComponent() { position = Vector3.Zero },
                    new RotationComponent() { orientation = Quaternion.Identity },
                    new TurntableComponent()
                );

            model = OBJ.LoadFromFile("resources/models/character.obj", "character");
            tex = Texture2D.LoadFromFile("resources/textures/character_clothes.png", "character");
            model.Material = new Material()
            {
                Name = shader.Name,
                Shader = shader,
                DiffuseTexture = tex,
                Roughness = 1f,
                Metallic = 0f
            };
            model.Bind();

            Engine.ECS.CreateEntity("character", "",
                    new RenderComponent() { mesh = model },
                    new RigidbodyComponent() { mass = 1f, restitution = 0f, drag = 3f, resistance = 1f, velocity = Vector3.Zero },
                    new CollisionComponent() { size = 0.5f },
                    new PositionComponent() { position = new Vector3(0f, 0f, -5f) },
                    new RotationComponent() { orientation = Quaternion.Identity },
                    new TurntableComponent()
                );

            tex = Texture2D.LoadFromFile("resources/textures/astronaut.png", "astronaut");
            CreateBillboardEntity(shader, tex, new Vector3(4f, 0f, -4f), Vector3.One * 2.5f);

            tex = Texture2D.LoadFromFile("resources/textures/chort.png", "chort");
            CreateBillboardEntity(shader, tex, new Vector3(6f, 0f, -4f), Vector3.One * 2.5f);

            tex = Texture2D.LoadFromFile("resources/textures/harold.png", "harold");
            CreateBillboardEntity(shader, tex, new Vector3(8f, 0f, -4f), Vector3.One * 2.5f);

            tex = Texture2D.LoadFromFile("resources/textures/hubert.png", "hubert");
            CreateBillboardEntity(shader, tex, new Vector3(10f, 0f, -4f), Vector3.One * 2.5f);

            tex = Texture2D.LoadFromFile("resources/textures/melvin.png", "melvin");
            CreateBillboardEntity(shader, tex, new Vector3(12f, 0f, -4f), Vector3.One * 2.5f);

            tex = Texture2D.LoadFromFile("resources/textures/woman.png", "woman");
            CreateBillboardEntity(shader, tex, new Vector3(14f, 0f, -4f), Vector3.One * 2.5f);
        }

        private void ShowGui()
        {
            if (showControls)
            {
                ImGui.SetNextWindowPos(new Vector2(0, 200));
                ImGui.Begin("Controls", WindowFlagPresets.FLAT);
                    ImGui.Text($"Console ~");
                    ImGui.Text($"Controls F1");
                    ImGui.Text($"Debug F2");
                    ImGui.Text($"- Stats F3");
                    ImGui.Text($"- Profiler F4");
                    ImGui.Text($"Timescale + -");

                    ImGui.Separator();

                    ImGui.Text($"Speed+ SHIFT");
                    ImGui.Text($"Move WASD");
                    ImGui.Text($"Up SPACE");
                    ImGui.Text($"Down CTRL");
                    ImGui.Text($"Roll QE");
                    ImGui.Text($"MouseLook TAB");
                    ImGui.Text($"Zoom C");

                    ImGui.Separator();

                    ImGui.Text($"Cube Rain F5");
                    ImGui.Text($"Spawn Cubes F6");
                    ImGui.Text($"Exit ESC");
                ImGui.End();
            }
        }

        private void Update()
        {
            //  The camera should behave independent of the engine delta, which is scaled by the timescale
            float delta = Engine.MainWindow.DeltaTime;

            if (raining)
                CreateEntityCubes(1);

            if (Input.IsKeyDown(Keys.Escape))
                Engine.Shutdown();

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
                Engine.Timescale += 0.5f * delta;

            if (Input.IsKeyDown(Keys.Minus))
                Engine.Timescale -= 0.5f * delta;

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
                cameraSpeed *= 7f;
            else if (Input.IsKeyReleased(Keys.LeftShift))
                cameraSpeed /= 7f;

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
}