using System;
using System.Reflection;

namespace Swordfish.Library.Reflection
{
    internal struct TypedBindingFlags
    {
        public Type Type;

        public BindingFlags BindingFlags;

        public TypedBindingFlags(Type type, BindingFlags bindingFlags)
        {
            Type = type;
            BindingFlags = bindingFlags;
        }
    }
}
