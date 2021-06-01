using OpenTK.Mathematics;
using waywardbeyond;

namespace Swordfish.Rendering
{
    public class Line
    {
        public Vector3 start;
        public Vector3 end;

        public Matrix4 matrix;

        public Line(Vector3 start, Vector3 end)
        {
            this.start = start;
            this.end = end;

            matrix = Matrix4.CreateTranslation(start);
        }

        public static void Draw(Vector3 start, Vector3 end)
        {
            Application.MainWindow.lines.Add(new Line(start, end));
        }
    }
}