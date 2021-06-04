using System;

namespace Swordfish.ECS_OLD
{
    /// <summary>
    /// Add this attribute to any struct to mark as an ECS component
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Struct)]
    public class ComponentAttribute : Attribute {}
}
