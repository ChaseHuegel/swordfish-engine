namespace Swordfish.Types.Constraints;

public class CenterConstraint : IConstraint
{
    public float GetValue(float max)
    {
        //  top-left is the origin so a centered value is 1/4 not 1/2
        return max * 0.25f;
    }
}
