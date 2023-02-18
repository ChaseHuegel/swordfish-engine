using System;
namespace Swordfish.Library.Types
{
    public class ValueFieldModifier : ValueFieldModifier<string>
    {
        public ValueFieldModifier(string identifier, Modifier modifier, float amount) : base(identifier, modifier, amount)
        {
        }
    }

    public class ValueFieldModifier<TIdentifier> : IEquatable<ValueFieldModifier<TIdentifier>>, IComparable<ValueFieldModifier<TIdentifier>>
    {
        public readonly TIdentifier Identifier;
        public readonly Modifier Modifier;
        public readonly float Amount;

        public ValueFieldModifier(TIdentifier identifier, Modifier modifier, float amount)
        {
            Identifier = identifier;
            Modifier = modifier;
            Amount = amount;
        }

        public void Apply(ref float value)
        {
            switch (Modifier)
            {
                case Modifier.ADDITION:
                    value += Amount;
                    break;
                case Modifier.SUBTRACTION:
                    value -= Amount;
                    break;
                case Modifier.MULTIPLY:
                    value *= Amount;
                    break;
                case Modifier.DIVIDE:
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
                return Equals(valueField);

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
}
