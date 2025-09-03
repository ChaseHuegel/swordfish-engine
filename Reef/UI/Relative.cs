using Reef.Constraints;

namespace Reef.UI;

public readonly struct Relative(float value) : IConstraint
{
    private readonly float _value = value;
    
    public int Calculate(int value)
    {
        return (int)(_value * value);
    }
}