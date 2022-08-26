namespace Swordfish.Types.Constraints;

public class AbsoluteConstraint : IConstraint
{
    private float value;

    public AbsoluteConstraint(float value)
    {
        this.value = value;
    }

    public float GetValue(float max)
    {
        return value;
    }
}
