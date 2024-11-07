using System;
namespace Swordfish.Library.Types;

// ReSharper disable once UnusedType.Global
public class ValueFieldModifier(in string identifier, in Modifier modifier, in float amount)
    : ValueFieldModifier<string>(identifier, modifier, amount);

public class ValueFieldModifier<TIdentifier>(in TIdentifier identifier, in Modifier modifier, in float amount)
    : IEquatable<ValueFieldModifier<TIdentifier>>, IComparable<ValueFieldModifier<TIdentifier>>
{
    public readonly TIdentifier Identifier = identifier;
    public readonly Modifier Modifier = modifier;
    public readonly float Amount = amount;

    public void Apply(ref float value)
    {
        switch (Modifier)
        {
            case Modifier.Addition:
                value += Amount;
                break;
            case Modifier.Subtract:
                value -= Amount;
                break;
            case Modifier.Multiply:
                value *= Amount;
                break;
            case Modifier.Divide:
                value /= Amount;
                break;
        }
    }

    public int CompareTo(ValueFieldModifier<TIdentifier> other)
    {
        return Modifier.CompareTo(other.Modifier);
    }

    public override bool Equals(object obj)
    {
        if (obj is ValueFieldModifier<TIdentifier> valueField)
        {
            return Equals(valueField);
        }

        return false;
    }

    public bool Equals(ValueFieldModifier<TIdentifier> other)
    {
        return Identifier.Equals(other.Identifier);
    }

    public override int GetHashCode()
    {
        return Identifier.GetHashCode();
    }
}