using Swordfish.Library.Types;
using Swordfish.Library.Util;

namespace Swordfish.Library.Collections
{
    public class ValueField
    {
        public string Name
        {
            get => NameBinding.Get();
            set => NameBinding.Set(value);
        }

        public float MaxValue
        {
            get => MaxValueBinding.Get();
            set
            {
                float oldMax = MaxValueBinding.Get();
                float newMax = value > 0f ? value : float.MaxValue;
                MaxValueBinding.Set(newMax);

                if (oldMax > 0f && oldMax != float.MaxValue && newMax > 0f && newMax != float.MaxValue)
                    Value *= newMax / oldMax;
            }
        }

        public float Value
        {
            get => ValueBinding.Get();
            set
            {
                float newValue = MathS.Clamp(value, 0f, MaxValue);
                ValueBinding.Set(newValue);
            }
        }

        public DataBinding<string> NameBinding { get; private set; }
        public DataBinding<float> MaxValueBinding { get; private set; }
        public DataBinding<float> ValueBinding { get; private set; }

        public ValueField(string name, float value = 1.0f, float max = 0.0f)
        {
            NameBinding = new DataBinding<string>();
            MaxValueBinding = new DataBinding<float>();
            ValueBinding = new DataBinding<float>();

            Name = name;
            MaxValue = max;
            Value = value;
        }

        public bool IsMax() => Value == MaxValue;
        public float CalculatePercent() => Value / MaxValue;

        public ValueField Add(float amount)
        {
            Value += amount;
            return this;
        }

        public ValueField Remove(float amount)
        {
            Value -= amount;
            return this;
        }

        public ValueField Maximize()
        {
            Value = MaxValue;
            return this;
        }

        public ValueField Clear()
        {
            Value = 0;
            return this;
        }

        public float PeekAdd(float amount)
        {
            return MathS.Clamp(Value + amount, 0, MaxValue);
        }

        public float PeekRemove(float amount)
        {
            return MathS.Clamp(Value - amount, 0, MaxValue);
        }

        public override bool Equals(object obj)
        {
            if (obj is string str)
                return Name.Equals(str);

            if (obj is ValueField atr)
                return Name.Equals(atr.Name);

            return false;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }

}