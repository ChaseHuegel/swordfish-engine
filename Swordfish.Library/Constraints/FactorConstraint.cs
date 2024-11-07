namespace Swordfish.Library.Constraints;

public class FactorConstraint(in float value) : IConstraint
{
    private readonly float _value = value;

    public float GetValue(float max)
    {
        return max / _value;
    }
}