namespace Swordfish.Types.Constraints;

public class RelativeConstraint : IConstraint
{
    private float value;

    public RelativeConstraint(float value)
    {
        this.value = value;
    }

    public float GetValue(float max)
    {
        return max * value;
    }
}
