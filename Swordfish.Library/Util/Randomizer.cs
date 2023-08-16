using System;
using System.Collections.Generic;
using System.Linq;

namespace Swordfish.Library.Util
{
    public class Randomizer
    {
        private Random Random;

        public Randomizer()
        {
            Random = new Random();
        }

        public float NextFloat()
        {
            return (float)Random.NextDouble();
        }

        public double NextDouble()
        {
            return Random.NextDouble();
        }

        public int NextInt()
        {
            return Random.Next();
        }

        public int NextInt(int maxValue)
        {
            return Random.Next(maxValue);
        }

        public int NextInt(int minValue, int maxValue)
        {
            return Random.Next(minValue, maxValue);
        }

        public T Select<T>(params T[] items)
        {
            int index = Random.Next(items.Length);
            return items[index];
        }

        public T Select<T>(IEnumerable<T> enumerable)
        {
            int index = Random.Next(enumerable.Count());
            return enumerable.ElementAt(index);
        }
    }
}
