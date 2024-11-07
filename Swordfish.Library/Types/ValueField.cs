using System.Collections.Generic;
using Swordfish.Library.Util;

namespace Swordfish.Library.Types
{
    public class ValueField : ValueField<string>
    {
        public ValueField(string identifier, float value = 1f, float max = 0) : base(identifier, value, max)
        {
        }
    }

    public class ValueField<TIdentifier>
    {
        public TIdentifier Identifier { get; private set; }

        public float MaxValue
        {
            get
            {
                float value = MaxValueBinding.Get();
                foreach (var modifier in MaxValueModifiers)
                    modifier.Apply(ref value);

                return value;
            }
            set
            {
                float oldMax = MaxValueBinding.Get();
                float newMax = value > 0f ? value : float.MaxValue;
                MaxValueBinding.Set(newMax);

                if (oldMax > 0f && oldMax != float.MaxValue && newMax > 0f && newMax != float.MaxValue)
                    ValueBinding.Set(ValueBinding.Get() * newMax / oldMax);
            }
        }

        public float Value
        {
            get
            {
                float value = ValueBinding.Get();
                foreach (var modifier in ValueModifiers)
                    modifier.Apply(ref value);

                return MathS.Clamp(value, 0f, MaxValue);
            }
            set
            {
                float newValue = MathS.Clamp(value, 0f, MaxValue);
                ValueBinding.Set(newValue);
            }
        }

        public DataBinding<float> MaxValueBinding { get; private set; }
        public DataBinding<float> ValueBinding { get; private set; }

        private readonly List<ValueFieldModifier<TIdentifier>> MaxValueModifiers = new List<ValueFieldModifier<TIdentifier>>();
        private readonly List<ValueFieldModifier<TIdentifier>> ValueModifiers = new List<ValueFieldModifier<TIdentifier>>();

        public ValueField(TIdentifier identifier, float value = 1f, float max = 0f)
        {
            MaxValueBinding = new DataBinding<float>();
            ValueBinding = new DataBinding<float>();

            Identifier = identifier;
            MaxValue = max;
            Value = value;
        }

        public bool IsMax() => Value == MaxValue;
        public float CalculatePercent() => Value / MaxValue;

        public ValueField<TIdentifier> Add(float amount)
        {
            Value += amount;
            return this;
        }

        public ValueField<TIdentifier> Remove(float amount)
        {
            Value -= amount;
            return this;
        }

        public ValueField<TIdentifier> Maximize()
        {
            Value = MaxValue;
            return this;
        }

        public ValueField<TIdentifier> Clear()
        {
            Value = 0;
            return this;
        }

        public ValueField<TIdentifier> AddModifier(TIdentifier identifier, Modifier modifier, float amount)
        {
            return AddModifier(new ValueFieldModifier<TIdentifier>(identifier, modifier, amount));
        }

        public ValueField<TIdentifier> AddModifier(ValueFieldModifier<TIdentifier> modifier)
        {
            ValueModifiers.Add(modifier);
            ValueModifiers.Sort();
            return this;
        }

        public ValueField<TIdentifier> RemoveModifier(TIdentifier identifier)
        {
            ValueModifiers.RemoveAll(modifier => modifier.Identifier.Equals(identifier));
            return this;
        }

        public ValueField<TIdentifier> RemoveModifier(ValueFieldModifier<TIdentifier> modifier)
        {
            ValueModifiers.Remove(modifier);
            return this;
        }

        public ValueFieldModifier<TIdentifier>[] GetModifiers()
        {
            return ValueModifiers.ToArray();
        }

        public ValueField<TIdentifier> AddMaxModifier(TIdentifier identifier, Modifier modifier, float amount)
        {
            return AddMaxModifier(new ValueFieldModifier<TIdentifier>(identifier, modifier, amount));
        }

        public ValueField<TIdentifier> AddMaxModifier(ValueFieldModifier<TIdentifier> modifier)
        {
            MaxValueModifiers.Add(modifier);
            MaxValueModifiers.Sort();
            return this;
        }

        public ValueField<TIdentifier> RemoveMaxModifier(TIdentifier identifier)
        {
            MaxValueModifiers.RemoveAll(modifier => modifier.Identifier.Equals(identifier));
            return this;
        }

        public ValueField<TIdentifier> RemoveMaxModifier(ValueFieldModifier<TIdentifier> modifier)
        {
            MaxValueModifiers.Remove(modifier);
            return this;
        }

        public ValueFieldModifier<TIdentifier>[] GetMaxModifiers()
        {
            return MaxValueModifiers.ToArray();
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
            if (obj is TIdentifier str)
                return Identifier.Equals(str);

            if (obj is ValueField<TIdentifier> valueField)
                return Identifier.Equals(valueField.Identifier);

            return false;
        }

        public override int GetHashCode()
        {
            return Identifier.GetHashCode();
        }
    }

}