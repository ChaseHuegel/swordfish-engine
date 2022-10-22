using Swordfish.Library.Types;
using Swordfish.Library.Util;

namespace Swordfish.Library.Collections
{
    public class Property
    {
        public string Name
        {
            get => NameBinding.Get();
            set => NameBinding.Set(value);
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

        public float MaxValue
        {
            get => MaxValueBinding.Get();
            set
            {
                float oldMax = MaxValueBinding.Get();
                float newMax = value > 0f ? value : float.MaxValue;

                if (oldMax > 0f)
                    Value *= newMax / oldMax;

                MaxValueBinding.Set(newMax);
            }
        }

        public DataBinding<string> NameBinding { get; private set; }
        public DataBinding<float> ValueBinding { get; private set; }
        public DataBinding<float> MaxValueBinding { get; private set; }

        public Property(string name, float value = 1.0f, float max = 0.0f)
        {
            NameBinding = new DataBinding<string>();
            ValueBinding = new DataBinding<float>();
            MaxValueBinding = new DataBinding<float>();

            Name = name;
            Value = value;
            MaxValue = max;
        }

        public bool IsMax() => Value == MaxValue;
        public float CalculatePercent() => Value / MaxValue;

        public Property Add(float amount)
        {
            Value += amount;
            return this;
        }

        public Property Remove(float amount)
        {
            Value -= amount;
            return this;
        }

        public Property Maximize()
        {
            Value = MaxValue;
            return this;
        }

        public Property Clear()
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

            if (obj is Property atr)
                return Name.Equals(atr.Name);

            return false;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }

}