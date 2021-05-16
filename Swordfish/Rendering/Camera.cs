using System;
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

namespace Swordfish.Rendering
{
    public class Camera
    {
        public Transform transform;
        public Matrix4 view;

        public float FOV;

        public Camera(float fov = 70f)
        {
            this.FOV = fov;
            this.transform = new Transform();

            UpdateView();
        }

        public void UpdateView()
        {
            view = Matrix4.LookAt(transform.position, transform.position + transform.forward, transform.up);
        }

        public void Update()
        {
            UpdateView();
        }
    }
}