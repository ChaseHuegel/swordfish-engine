using System.Collections.Generic;
using Swordfish.Library.Util;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Types;

public class ValueField(in string identifier, in float value = 1f, in float max = 0) : ValueField<string>(identifier, value, max);

public class ValueField<TIdentifier>
{
    public TIdentifier Identifier { get; }
    
    public DataBinding<float> MaxValueBinding { get; }
    public DataBinding<float> ValueBinding { get; }

    private readonly List<ValueFieldModifier<TIdentifier>> _maxValueModifiers = [];
    private readonly List<ValueFieldModifier<TIdentifier>> _valueModifiers = [];

    public float MaxValue
    {
        get
        {
            float value = MaxValueBinding.Get();
            foreach (ValueFieldModifier<TIdentifier> modifier in _maxValueModifiers)
            {
                modifier.Apply(ref value);
            }

            return value;
        }
        set
        {
            float oldMax = MaxValueBinding.Get();
            float newMax = value > 0f ? value : float.MaxValue;
            MaxValueBinding.Set(newMax);

            // ReSharper disable CompareOfFloatsByEqualityOperator
            if (oldMax > 0f && oldMax != float.MaxValue && newMax > 0f && newMax != float.MaxValue)
            {
                ValueBinding.Set(ValueBinding.Get() * newMax / oldMax);
            }
            // ReSharper restore CompareOfFloatsByEqualityOperator
        }
    }

    public float Value
    {
        get
        {
            float value = ValueBinding.Get();
            foreach (ValueFieldModifier<TIdentifier> modifier in _valueModifiers)
            {
                modifier.Apply(ref value);
            }

            return MathS.Clamp(value, 0f, MaxValue);
        }
        set
        {
            float newValue = MathS.Clamp(value, 0f, MaxValue);
            ValueBinding.Set(newValue);
        }
    }

    public ValueField(TIdentifier identifier, float value = 1f, float max = 0f)
    {
        MaxValueBinding = new DataBinding<float>();
        ValueBinding = new DataBinding<float>();

        Identifier = identifier;
        MaxValue = max;
        Value = value;
    }

    // ReSharper disable once CompareOfFloatsByEqualityOperator
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

    // ReSharper disable once UnusedMethodReturnValue.Global
    public ValueField<TIdentifier> AddModifier(TIdentifier identifier, Modifier modifier, float amount)
    {
        return AddModifier(new ValueFieldModifier<TIdentifier>(identifier, modifier, amount));
    }

    public ValueField<TIdentifier> AddModifier(ValueFieldModifier<TIdentifier> modifier)
    {
        _valueModifiers.Add(modifier);
        _valueModifiers.Sort();
        return this;
    }

    public ValueField<TIdentifier> RemoveModifier(TIdentifier identifier)
    {
        _valueModifiers.RemoveAll(modifier => modifier.Identifier.Equals(identifier));
        return this;
    }

    public ValueField<TIdentifier> RemoveModifier(ValueFieldModifier<TIdentifier> modifier)
    {
        _valueModifiers.Remove(modifier);
        return this;
    }

    public ValueFieldModifier<TIdentifier>[] GetModifiers()
    {
        return _valueModifiers.ToArray();
    }

    public ValueField<TIdentifier> AddMaxModifier(TIdentifier identifier, Modifier modifier, float amount)
    {
        return AddMaxModifier(new ValueFieldModifier<TIdentifier>(identifier, modifier, amount));
    }

    public ValueField<TIdentifier> AddMaxModifier(ValueFieldModifier<TIdentifier> modifier)
    {
        _maxValueModifiers.Add(modifier);
        _maxValueModifiers.Sort();
        return this;
    }

    public ValueField<TIdentifier> RemoveMaxModifier(TIdentifier identifier)
    {
        _maxValueModifiers.RemoveAll(modifier => modifier.Identifier.Equals(identifier));
        return this;
    }

    public ValueField<TIdentifier> RemoveMaxModifier(ValueFieldModifier<TIdentifier> modifier)
    {
        _maxValueModifiers.Remove(modifier);
        return this;
    }

    public ValueFieldModifier<TIdentifier>[] GetMaxModifiers()
    {
        return _maxValueModifiers.ToArray();
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
        {
            return Identifier.Equals(str);
        }

        if (obj is ValueField<TIdentifier> valueField)
        {
            return Identifier.Equals(valueField.Identifier);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Identifier.GetHashCode();
    }
}