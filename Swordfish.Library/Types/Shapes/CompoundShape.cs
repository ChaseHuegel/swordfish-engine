using System.Numerics;

namespace Swordfish.Library.Types.Shapes
{
    public struct CompoundShape : IShape
    {
        public IShape[] Shapes;
        public Vector3[] Positions;
        public Quaternion[] Orientations;

        public CompoundShape(IShape[] shapes, Vector3[] positions, Quaternion[] orientations)
        {
            Shapes = shapes;
            Positions = positions;
            Orientations = orientations;
        }
    }
}