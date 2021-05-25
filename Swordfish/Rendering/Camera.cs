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
using System.Drawing.Drawing2D;

namespace Swordfish.Rendering
{
    public class Camera
    {
        public Transform transform;
        public Matrix4 view;

        public float FOV;

        public Camera(Vector3 position, Quaternion rotation, float fov = 70f)
        {
            this.FOV = fov;
            this.transform = new Transform(position, rotation);

            UpdateView();
        }

        public void UpdateView()
        {
            // view = Matrix4.LookAt(transform.position, transform.position + transform.forward, transform.up);
            view = Matrix4.CreateTranslation(transform.position) * Matrix4.CreateFromQuaternion(transform.rotation);
        }

        public void Update()
        {
            UpdateView();
        }
    }
}