using Swordfish.Library.Types;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Constraints;

public class AbsoluteConstraint : IConstraint
{
    private readonly float _value;
    private readonly DataBinding<float> _binding;

    public AbsoluteConstraint(float value)
    {
        _value = value;
    }

    public AbsoluteConstraint(DataBinding<float> binding)
    {
        _binding = binding;
    }

    public float GetValue(float max)
    {
        return _binding?.Get() ?? _value;
    }
}