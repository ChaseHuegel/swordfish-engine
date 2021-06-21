using ImGuiNET;

using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

using Swordfish;
using Swordfish.Diagnostics;
using Swordfish.ECS;
using Swordfish.Rendering;
using Swordfish.Rendering.Shapes;
using Swordfish.Rendering.UI;

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

        public void CreateEntityCubes(int count)
        {
            Entity entity;
            for (int i = 0; i < count; i++)
            {
                entity = Engine.ECS.CreateEntity();
                if (entity == null) continue;

                Engine.ECS.Attach<RenderComponent>(entity, new RenderComponent() { mesh = new Cube() })
                    .Attach<RigidbodyComponent>(entity, new RigidbodyComponent() { mass = Engine.Random.Next(2, 10), resistance = 1, drag = 1, velocity = Vector3.Zero })
                    .Attach<CollisionComponent>(entity, new CollisionComponent() { size = 1f })
                    .Attach<PositionComponent>(entity, new PositionComponent() { position = new Vector3(Engine.Random.Next(-100, 100), 10, Engine.Random.Next(-100, 100)) })
                    .Attach<RotationComponent>(entity, new RotationComponent() { orientation = Quaternion.FromEulerAngles(Engine.Random.Next(360), Engine.Random.Next(360), Engine.Random.Next(360)) });
            }
        }

        private void Start()
        {
            Debug.Enabled = true;
        }

        private void ShowGui()
        {
            if (showControls)
            {
                ImGui.SetNextWindowPos(new Vector2(0, 120));
                ImGui.Begin("Controls", WindowFlagPresets.FLAT);
                    ImGui.Text($"Console ~");
                    ImGui.Text($"Controls F1");
                    ImGui.Text($"Debug F2");
                    ImGui.Text($"- Stats F3");
                    ImGui.Text($"- Profiler F4");

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

            if (Input.IsKeyDown(Keys.W))
                Camera.Main.transform.position += Camera.Main.transform.forward * cameraSpeed * Engine.DeltaTime;
            if (Input.IsKeyDown(Keys.S))
                Camera.Main.transform.position -= Camera.Main.transform.forward * cameraSpeed * Engine.DeltaTime;

            if (Input.IsKeyDown(Keys.A))
                Camera.Main.transform.position += Camera.Main.transform.right * cameraSpeed * Engine.DeltaTime;
            if (Input.IsKeyDown(Keys.D))
                Camera.Main.transform.position -= Camera.Main.transform.right * cameraSpeed * Engine.DeltaTime;

            if (Input.IsKeyDown(Keys.Space))
                Camera.Main.transform.position += Camera.Main.transform.up * cameraSpeed * Engine.DeltaTime;
            if (Input.IsKeyDown(Keys.LeftControl))
                Camera.Main.transform.position -= Camera.Main.transform.up * cameraSpeed * Engine.DeltaTime;

            if (Input.IsKeyDown(Keys.E))
                Camera.Main.transform.Rotate(Vector3.UnitZ, 40 * Engine.DeltaTime);
            if (Input.IsKeyDown(Keys.Q))
                Camera.Main.transform.Rotate(Vector3.UnitZ, -40 * Engine.DeltaTime);

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