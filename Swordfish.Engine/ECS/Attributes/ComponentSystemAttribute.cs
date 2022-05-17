using System;

namespace Swordfish.Engine.ECS
{
    /// <summary>
    /// Add this attribute to any class to mark as an ECS system
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class ComponentSystemAttribute : Attribute
    {
        public Type[] filter = null;

        public ComponentSystemAttribute(params Type[] filter)
        {
            this.filter = filter;
        }
    }
}
