using System;

namespace Reef;

/// <summary>
///     Marks a generated markup component so other assemblies can resolve the
///     component by its markup tag name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ReefComponentAttribute(string name, Type? contextType = null) : Attribute
{
    /// <summary>The markup tag name of the component.</summary>
    public string Name { get; } = name;

    /// <summary>The declared context type, or null when the component has no context.</summary>
    public Type? ContextType { get; } = contextType;
}
