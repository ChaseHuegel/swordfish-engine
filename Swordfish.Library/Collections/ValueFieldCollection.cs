using System.Collections.Generic;

namespace Swordfish.Library.Collections
{
    public class ValueFieldCollection
    {
        private readonly Dictionary<string, ValueField> Items = new Dictionary<string, ValueField>();

        public bool Contains(string name)
        {
            return Items.ContainsKey(name);
        }

        public float ValueOf(string name)
        {
            return Items.TryGetValue(name, out ValueField attribute) ? attribute.Value : 0f;
        }

        public float MaxValueOf(string name)
        {
            return Items.TryGetValue(name, out ValueField attribute) ? attribute.MaxValue : 0f;
        }

        public float CalculatePercentOf(string name)
        {
            return Items.TryGetValue(name, out ValueField attribute) ? attribute.CalculatePercent() : 0f;
        }

        public ValueField Get(string name)
        {
            return Items.TryGetValue(name, out ValueField attribute) ? attribute : null;
        }

        public ValueField Add(string name, float value, float max = 0f)
        {
            if (!Items.ContainsKey(name))
            {
                var attribute = new ValueField(name, value, max);
                Items.Add(name, attribute);
                return attribute;
            }

            return null;
        }

        public bool TryAdd(string name, float value, float max = 0f)
        {
            if (Items.ContainsKey(name))
                return false;
            else
            {
                Items.Add(name, new ValueField(name, value, max));
                return true;
            }
        }

        public bool Remove(string name)
        {
            return Items.Remove(name);
        }

        public ValueField GetOrAdd(string name, float value, float max = 0f)
        {
            return Get(name) ?? Add(name, value, max);
        }
    }

}