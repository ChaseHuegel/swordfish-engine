using Swordfish.Library.Types;

namespace Swordfish.Types.Constraints;

public class AbsoluteConstraint : IConstraint
{
    private float value;
    private DataBinding<float>? binding;

    public AbsoluteConstraint(float value)
    {
        this.value = value;
    }

    public AbsoluteConstraint(DataBinding<float> binding)
    {
        this.binding = binding;
    }

    public float GetValue(float max)
    {
        return binding != null ? binding.Get() : value;
    }
}
