namespace Swordfish.Library.Types.Constraints
{
    public class FillConstraint : IConstraint
    {
        public float GetValue(float max)
        {
            return max;
        }
    }
}