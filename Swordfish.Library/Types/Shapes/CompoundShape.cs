using System.Numerics;

namespace Swordfish.Library.Types.Shapes
{
    public unsafe readonly struct CompoundShape
    {
        public readonly Shape[] Shapes;
        public readonly Vector3[] Positions;
        public readonly Quaternion[] Orientations;

        public CompoundShape(Shape[] shapes, Vector3[] positions, Quaternion[] orientations)
        {
            Shapes = shapes;
            Positions = positions;
            Orientations = orientations;
        }
    }
}