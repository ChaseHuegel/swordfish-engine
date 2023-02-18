using System.Collections.Generic;
using Swordfish.Library.Types;

namespace Swordfish.Library.Collections
{
    public class ValueFieldCollection : ValueFieldCollection<string>
    {
    }

    public class ValueFieldCollection<TIdentifier>
    {
        private readonly Dictionary<TIdentifier, ValueField<TIdentifier>> Items = new Dictionary<TIdentifier, ValueField<TIdentifier>>();

        public bool Contains(TIdentifier identifier)
        {
            return Items.ContainsKey(identifier);
        }

        public float ValueOf(TIdentifier identifier)
        {
            return Items.TryGetValue(identifier, out ValueField<TIdentifier> attribute) ? attribute.Value : 0f;
        }

        public float MaxValueOf(TIdentifier identifier)
        {
            return Items.TryGetValue(identifier, out ValueField<TIdentifier> attribute) ? attribute.MaxValue : 0f;
        }

        public float CalculatePercentOf(TIdentifier identifier)
        {
            return Items.TryGetValue(identifier, out ValueField<TIdentifier> attribute) ? attribute.CalculatePercent() : 0f;
        }

        public ValueField<TIdentifier> Get(TIdentifier identifier)
        {
            return Items.TryGetValue(identifier, out ValueField<TIdentifier> attribute) ? attribute : null;
        }

        public ValueField<TIdentifier> Add(TIdentifier identifier, float value, float max = 0f)
        {
            if (!Items.ContainsKey(identifier))
            {
                var attribute = new ValueField<TIdentifier>(identifier, value, max);
                Items.Add(identifier, attribute);
                return attribute;
            }

            return null;
        }

        public bool TryAdd(TIdentifier identifier, float value, float max = 0f)
        {
            if (Items.ContainsKey(identifier))
                return false;
            else
            {
                Items.Add(identifier, new ValueField<TIdentifier>(identifier, value, max));
                return true;
            }
        }

        public ValueField<TIdentifier> AddOrUpdate(TIdentifier identifier, float value, float max = 0f)
        {
            if (Items.TryGetValue(identifier, out ValueField<TIdentifier> field))
            {
                field.MaxValue = max;
                field.Value = value;
                return field;
            }
            else
            {
                field = new ValueField<TIdentifier>(identifier, value, max);
                Items.Add(identifier, field);
                return field;
            }
        }

        public bool Remove(TIdentifier name)
        {
            return Items.Remove(name);
        }

        public ValueField<TIdentifier> GetOrAdd(TIdentifier name, float value, float max = 0f)
        {
            return Get(name) ?? Add(name, value, max);
        }
    }

}