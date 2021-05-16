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

namespace Swordfish
{
    public class Transform
    {
        public Transform parent = null;
        public Vector3 position = new Vector3(0f, 0f, 0f);
        public Vector3 forward  = new Vector3(0f, 0f, -1f);
        public Vector3 up       = new Vector3(0f, 1f, 0f);
        public Vector3 right    = new Vector3(-1f, 0f, 0f);
    }
}