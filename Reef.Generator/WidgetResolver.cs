using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Reef.Generator;

internal sealed class WidgetMethod
{
    public IMethodSymbol Method = null!;
    public INamedTypeSymbol ContainingType = null!;
}

internal static class WidgetResolver
{
    public static INamespaceSymbol? FindNamespace(Compilation compilation, string namespaceName)
    {
        INamespaceSymbol current = compilation.GlobalNamespace;
        string[] parts = namespaceName.Split('.');
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
            {
                continue;
            }

            INamespaceSymbol? next = null;
            foreach (INamespaceSymbol member in current.GetNamespaceMembers())
            {
                if (string.Equals(member.Name, parts[i], StringComparison.Ordinal))
                {
                    next = member;
                    break;
                }
            }

            if (next == null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    public static List<WidgetMethod> FindWidgets(
        Compilation compilation,
        INamespaceSymbol namespaceSymbol,
        string tagName)
    {
        var results = new List<WidgetMethod>();
        CollectWidgets(compilation, namespaceSymbol, tagName, results);
        return results;
    }

    private static void CollectWidgets(
        Compilation compilation,
        INamespaceSymbol namespaceSymbol,
        string tagName,
        List<WidgetMethod> results)
    {
        foreach (INamespaceSymbol childNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            CollectWidgets(compilation, childNamespace, tagName, results);
        }

        foreach (INamedTypeSymbol type in namespaceSymbol.GetTypeMembers())
        {
            CollectWidgets(compilation, type, tagName, results);
        }
    }

    private static void CollectWidgets(
        Compilation compilation,
        INamedTypeSymbol type,
        string tagName,
        List<WidgetMethod> results)
    {
        foreach (ISymbol member in type.GetMembers(tagName))
        {
            if (member is not IMethodSymbol method
                || method.MethodKind != MethodKind.Ordinary
                || !method.IsStatic
                || method.Parameters.Length == 0)
            {
                continue;
            }

            if (!IsUIBuilder(method.Parameters[0].Type))
            {
                continue;
            }

            if (!compilation.IsSymbolAccessibleWithin(method, compilation.Assembly))
            {
                continue;
            }

            results.Add(new WidgetMethod
            {
                Method = method,
                ContainingType = type,
            });
        }

        foreach (INamedTypeSymbol nested in type.GetTypeMembers())
        {
            CollectWidgets(compilation, nested, tagName, results);
        }
    }

    public static bool IsUIBuilder(ITypeSymbol type)
    {
        return type is INamedTypeSymbol named
               && named.OriginalDefinition.ToDisplayString() == "Reef.UIBuilder<TRendererData>";
    }

    public static string NormalizeParameterName(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c == '-' || c == '_' || c == ' ')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}

internal static class WidgetFacts
{
    public static bool IsApplicable(ITypeSymbol? rendererType, IMethodSymbol method)
    {
        if (method.Parameters.Length == 0)
        {
            return false;
        }

        if (method.Parameters[0].Type is not INamedTypeSymbol uiType
            || uiType.TypeArguments.Length != 1)
        {
            return false;
        }

        ITypeSymbol argument = uiType.TypeArguments[0];
        if (argument.TypeKind == TypeKind.TypeParameter)
        {
            return true;
        }

        if (rendererType == null)
        {
            return argument.SpecialType == SpecialType.System_Object;
        }

        return SymbolEqualityComparer.Default.Equals(argument, rendererType);
    }

    public static bool IsDisposable(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Void)
        {
            return false;
        }

        if (type is INamedTypeSymbol named)
        {
            if (named.ToDisplayString() == "System.IDisposable")
            {
                return true;
            }

            for (var i = 0; i < named.AllInterfaces.Length; i++)
            {
                if (named.AllInterfaces[i].ToDisplayString() == "System.IDisposable")
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool TryGetControlResult(ITypeSymbol type, out ITypeSymbol? valueType)
    {
        valueType = null;
        if (type is not INamedTypeSymbol named || named.TypeArguments.Length != 1)
        {
            return false;
        }

        if (!string.Equals(named.Name, "ControlResult", StringComparison.Ordinal))
        {
            return false;
        }

        bool hasChanged = false;
        bool hasValue = false;
        foreach (ISymbol member in named.GetMembers())
        {
            if (member is IPropertySymbol property && property.Name == "Changed")
            {
                hasChanged = true;
            }
            else if (member is IPropertySymbol valueProperty && valueProperty.Name == "Value")
            {
                hasValue = true;
                valueType = valueProperty.Type;
            }
        }

        return hasChanged && hasValue;
    }
}
