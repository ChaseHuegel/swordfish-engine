using System;

namespace Swordfish.Library.Annotations
{
    [AttributeUsage(
        AttributeTargets.Field |
        AttributeTargets.Property |
        AttributeTargets.Parameter |
        AttributeTargets.ReturnValue
    )]
    public class NotNullAttribute : Attribute
    {
    }
}
