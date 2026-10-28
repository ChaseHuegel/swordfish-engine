using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Reef.Generator;

internal abstract class RmlNode
{
    public Location Location = Location.None;
    public int Line;
    public int Column;
}

internal sealed class RmlAttribute
{
    public string Name = string.Empty;
    public string Prefix = string.Empty;
    public string NamespaceUri = string.Empty;
    public string Value = string.Empty;
    public Location Location = Location.None;
    public int Order;
    public int Line;
    public int Column;

    public bool IsClassDirective => NamespaceUri == "Reef.Class";

    public string QualifiedName
    {
        get
        {
            return Prefix.Length > 0 ? Prefix + ":" + Name : Name;
        }
    }
}

internal sealed class RmlElementNode : RmlNode
{
    public string Tag = string.Empty;
    public string Prefix = string.Empty;
    public string NamespaceUri = string.Empty;
    public readonly List<RmlAttribute> Attributes = [];
    public readonly List<RmlNode> Children = [];
    public string? TextContent;
    public bool PreserveWhitespace;
    public bool HasElementChildren;

    public RmlAttribute? FindAttribute(string name)
    {
        for (var i = 0; i < Attributes.Count; i++)
        {
            RmlAttribute attribute = Attributes[i];
            if (attribute.NamespaceUri.Length == 0 && string.Equals(attribute.Name, name, System.StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        return null;
    }

    public RmlAttribute? FindXmlAttribute(string localName)
    {
        for (var i = 0; i < Attributes.Count; i++)
        {
            RmlAttribute attribute = Attributes[i];
            if (attribute.NamespaceUri == "http://www.w3.org/XML/1998/namespace"
                && string.Equals(attribute.Name, localName, System.StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        return null;
    }

    public string? GetAttributeText(string name)
    {
        return FindAttribute(name)?.Value;
    }

    public bool HasClass(string name)
    {
        string? classes = GetAttributeText("class");
        if (classes == null || classes.Trim().Length == 0)
        {
            return false;
        }

        string[] parts = classes.Split(new[] { ' ', '\t', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            if (string.Equals(parts[i], name, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class RmlIfNode : RmlNode
{
    public string? Condition;
    public readonly List<RmlNode> Children = [];
}

internal sealed class RmlElseIfNode : RmlNode
{
    public string? Condition;
    public readonly List<RmlNode> Children = [];
}

internal sealed class RmlElseNode : RmlNode
{
    public readonly List<RmlNode> Children = [];
}

internal sealed class RmlConditionalNode : RmlNode
{
    public readonly List<RmlConditionalBranch> Branches = [];
}

internal sealed class RmlConditionalBranch
{
    public string? Condition;
    public Location Location = Location.None;
    public readonly List<RmlNode> Children = [];
}

internal sealed class RmlForEachNode : RmlNode
{
    public string? InExpression;
    public string? AsName;
    public string? IndexName;
    public readonly List<RmlNode> Children = [];
    public readonly List<RmlNode> EmptyChildren = [];
    public Location EmptyLocation = Location.None;
}

internal sealed class RmlContentNode : RmlNode
{
    public string? Name;
    public readonly List<RmlNode> Children = [];
}

internal sealed class RmlEmptyNode : RmlNode
{
    public readonly List<RmlNode> Children = [];
}

internal sealed class RmlDocument
{
    public string FilePath = string.Empty;
    public string ClassName = string.Empty;
    public string? Access;
    public string? ContextType;
    public string? RendererDataType;
    public string? NamespaceName;
    public string? DefaultNamespace;
    public bool HasClassNamespace;
    public Location Location = Location.None;
    public Location ClassLocation = Location.None;
    public readonly List<RmlNode> Roots = [];
    public readonly List<Diagnostic> Diagnostics = [];
    public readonly Dictionary<string, string> NamespacePrefixes = new(System.StringComparer.Ordinal);
}
