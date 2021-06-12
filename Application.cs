using System;
using System.Collections.Generic;
using ImGuiNET;
using OpenTK;
using OpenTK.Mathematics;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Swordfish;
using Swordfish.Rendering;
using Swordfish.Rendering.Shapes;
using Swordfish.Rendering.UI;
using Swordfish.ECS;
using ECSTest;

namespace source
{
    public class Application
    {
        static void Main(string[] args)
        {
            Application game = new Application();
            Engine.StartCallback = game.Start;
            Engine.UpdateCallback = game.Update;

            Engine.Initialize();
        }

        public Random rand = new Random();
        public float cameraSpeed = 12f;
        public List<Transform> cubes;

        private void Start()
        {
            Debug.Stats = true;

            CreateEntityCubes(1000);
        }

        public void CreateEntityCubes(int count)
        {
            Entity entity;
            for (int i = 0; i < count; i++)
            {
                entity = Engine.ECS.CreateEntity();

                Engine.ECS.Attach<RenderComponent>(entity, new RenderComponent() { mesh = new Cube() })
                    .Attach<PositionComponent>(entity, new PositionComponent() { position = new Vector3(rand.Next(-100, 100), rand.Next(-100, 100), rand.Next(-100, 100)) })
                    .Attach<RotationComponent>(entity, new RotationComponent() { orientation = Quaternion.FromEulerAngles(rand.Next(360), rand.Next(360), rand.Next(360)) });
            }
        }

        private void Update()
        {
            if (Input.IsKeyPressed(Keys.GraveAccent))
                Debug.Enabled = !Debug.Enabled;

            if (Input.IsKeyPressed(Keys.F1))
                Debug.Stats = !Debug.Stats;

            if (Input.IsKeyDown(Keys.Escape))
                Engine.Shutdown();

            if (Input.IsKeyPressed(Keys.F2))
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