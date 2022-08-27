namespace Swordfish.Types.Constraints;

public class FillConstraint : IConstraint
{
    public float GetValue(float max)
    {
        return max;
    }
}
