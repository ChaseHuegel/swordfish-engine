using System;

namespace Swordfish.ECS
{
    /// <summary>
    /// Add this attribute to any class to mark as an ECS system
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class ComponentSystemAttribute : Attribute
    {
        public Type[] mask = null;

        public ComponentSystemAttribute(params Type[] mask)
        {
            this.mask = mask;
        }
    }
}
