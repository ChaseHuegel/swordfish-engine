using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Swordfish.Library.Types
{
    public abstract class DynamicEnum<T> where T : DynamicEnum<T>
    {
        private static T s_Instance;
        private static T Instance => s_Instance ?? (s_Instance = (T)Activator.CreateInstance(typeof(T)));

        private ConcurrentDictionary<int, DynamicEnumValue> Values = new();

        protected DynamicEnum()
        {
            //  Use provided ID if available, otherwise fallback to auto assigning IDs based on count
            foreach (DynamicEnumValue value in Initialize())
                Values.TryAdd(Values.Count, new DynamicEnumValue(value.ID ?? Values.Count, value.Name));
        }

        protected abstract IEnumerable<DynamicEnumValue> Initialize();

        public static DynamicEnumValue[] GetValues() => Instance.Values.Values.ToArray();

        public static DynamicEnumValue Get(int id) => Instance.Values[id];

        public static DynamicEnumValue Get(string name) => Instance.Values.Values.FirstOrDefault(x => x.Name == name);
    }
}
