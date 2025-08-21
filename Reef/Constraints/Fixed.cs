namespace Reef.Constraints;

public readonly struct Fixed(int value) : IConstraint
{
    private readonly int _value = value;
    
    public int Calculate(int value)
    {
        return _value;
    }
}