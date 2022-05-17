using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

using Swordfish.Library.Types;

namespace Swordfish.Engine.Rendering
{
    public class Line
    {
        public Vector3 start;
        public Vector3 end;
        public Color color;

        private Matrix4 transform;

        private Shader shader;
        private int VBO, VAO;
        private float[] vertices;

        public Line(Vector3 start, Vector3 end, Color color)
        {
            this.start = start;
            this.end = end;
            this.color = color;

            transform = Matrix4.CreateTranslation(start);

            shader = Shaders.UNLIT.Get();

            vertices = new float[] {
                start.X, start.Y, start.Z,
                end.X, end.Y, end.Z
                };

            //  Setup VAO
            VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            //  Setup vertex buffer
            VBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(shader.GetAttribLocation("in_position"), 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(shader.GetAttribLocation("in_position"));

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
        }

        public void Draw(Matrix4 view, Matrix4 projection)
        {
            shader.Use();

            shader.SetMatrix4("view", view);
            shader.SetMatrix4("projection", projection);
            shader.SetMatrix4("transform", transform);
            shader.SetMatrix4("inversedTransform", transform.Inverted());
            shader.SetVec4("tint", color);

            GL.BindVertexArray(VAO);
            GL.DrawArrays(PrimitiveType.Lines, 0, vertices.Length);
            GL.BindVertexArray(0);
        }
    }
}