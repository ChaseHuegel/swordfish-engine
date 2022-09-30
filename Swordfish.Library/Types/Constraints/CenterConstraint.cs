namespace Swordfish.Library.Types.Constraints
{
    public class CenterConstraint : IConstraint
    {
        public float GetValue(float max)
        {
            return max * 0.5f;
        }
    }
}