using System;

namespace Swordfish.Library.Util
{
    public static class Reflection
    {
        public static bool HasAttribute<TType, TAttribute>() where TAttribute : Attribute
        {
            return HasAttribute(typeof(TType), typeof(TAttribute));
        }

        public static bool HasAttribute<TAttribute>(Type type) where TAttribute : Attribute
        {
            return HasAttribute(type, typeof(TAttribute));
        }

        public static bool HasAttribute(Type type, Type attribute)
        {
            return Attribute.GetCustomAttribute(type, attribute) != null;
        }

        public static bool TryGetAttribute<TType, TAttribute>(out TAttribute result) where TAttribute : Attribute
        {
            return TryGetAttribute(typeof(TType), out result);
        }

        public static bool TryGetAttribute<TAttribute>(Type type, out TAttribute result) where TAttribute : Attribute
        {
            bool hasAttribute = TryGetAttribute(type, typeof(TAttribute), out Attribute attribute);
            result = (TAttribute)attribute;
            return hasAttribute;
        }

        public static bool TryGetAttribute(Type type, Type attribute, out Attribute result)
        {
            result = Attribute.GetCustomAttribute(type, attribute);
            return result != null;
        }
    }
}
