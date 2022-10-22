using System.Collections.Generic;

namespace Swordfish.Library.Collections
{
    public class PropertyCollection
    {
        private readonly Dictionary<string, Property> Items = new Dictionary<string, Property>();

        public bool Contains(string name)
        {
            return Items.ContainsKey(name);
        }

        public float ValueOf(string name)
        {
            return Items.TryGetValue(name, out Property attribute) ? attribute.Value : 0f;
        }

        public float MaxValueOf(string name)
        {
            return Items.TryGetValue(name, out Property attribute) ? attribute.MaxValue : 0f;
        }

        public float CalculatePercentOf(string name)
        {
            return Items.TryGetValue(name, out Property attribute) ? attribute.CalculatePercent() : 0f;
        }

        public Property Get(string name)
        {
            return Items.TryGetValue(name, out Property attribute) ? attribute : null;
        }

        public Property Add(string name, float value, float max = 0f)
        {
            if (!Items.ContainsKey(name))
            {
                var attribute = new Property(name, value, max);
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
                Items.Add(name, new Property(name, value, max));
                return true;
            }
        }

        public bool Remove(string name)
        {
            return Items.Remove(name);
        }

        public Property GetOrAdd(string name, float value, float max = 0f)
        {
            return Get(name) ?? Add(name, value, max);
        }
    }

}