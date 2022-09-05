namespace Swordfish.Types.Constraints;

public class FactorConstraint : IConstraint
{
    private float value;

    public FactorConstraint(float value)
    {
        this.value = value;
    }

    public float GetValue(float max)
    {
        return max / value;
    }
}
