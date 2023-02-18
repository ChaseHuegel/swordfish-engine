using System;

namespace Swordfish.Library.Reflection
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class MemberOrderAttribute : Attribute
    {
        public int Index;

        public MemberOrderAttribute()
        {
            Index = int.MaxValue;
        }

        public MemberOrderAttribute(int index)
        {
            Index = index;
        }
    }
}
