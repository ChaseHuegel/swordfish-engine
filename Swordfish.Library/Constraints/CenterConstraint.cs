namespace Swordfish.Library.Constraints;

// ReSharper disable once UnusedType.Global
public class CenterConstraint : IConstraint
{
    public float GetValue(float max)
    {
        return max * 0.5f;
    }
}