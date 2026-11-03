using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace Reef.Generator;

internal static class TypeResolver
{
    public static ITypeSymbol? ResolveType(Compilation compilation, string typeName, string currentNamespace)
    {
        string name = typeName.Trim();
        if (name.StartsWith("global::", StringComparison.Ordinal))
        {
            name = name.Substring("global::".Length);
        }

        if (name.Length == 0)
        {
            return null;
        }

        INamedTypeSymbol? direct = compilation.GetTypeByMetadataName(name);
        if (direct != null)
        {
            return direct;
        }

        if (currentNamespace.Length > 0)
        {
            INamedTypeSymbol? local = compilation.GetTypeByMetadataName(currentNamespace + "." + name);
            if (local != null)
            {
                return local;
            }
        }

        string simpleName = name;
        int dot = simpleName.LastIndexOf('.');
        if (dot >= 0)
        {
            simpleName = simpleName.Substring(dot + 1);
        }

        var matches = new List<INamedTypeSymbol>();
        CollectTypes(compilation.GlobalNamespace, simpleName, matches);
        return matches.Count == 1 ? matches[0] : null;
    }

    public static bool IsAccessible(Compilation compilation, ISymbol symbol)
    {
        return compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly);
    }

    public static string InferNamespace(
        RmlDocument document,
        Compilation compilation,
        AnalyzerConfigOptionsProvider options)
    {
        if (document.NamespaceName != null)
        {
            return document.NamespaceName.Trim();
        }

        options.GlobalOptions.TryGetValue("build_property.RootNamespace", out string? rootNamespace);
        options.GlobalOptions.TryGetValue("build_property.ProjectDir", out string? projectDirectory);

        string root = string.IsNullOrWhiteSpace(rootNamespace)
            ? compilation.AssemblyName ?? string.Empty
            : rootNamespace!.Trim();

        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return root;
        }

        string projectDir = projectDirectory!.Trim();
        string filePath = document.FilePath;
        string relative;
        try
        {
            string fullBase = System.IO.Path.GetFullPath(projectDir)
                .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
                + System.IO.Path.DirectorySeparatorChar;
            string fullPath = System.IO.Path.GetFullPath(filePath);
            if (fullPath.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
            {
                relative = fullPath.Substring(fullBase.Length);
            }
            else
            {
                return root;
            }
        }
        catch (ArgumentException)
        {
            return root;
        }

        string? directory = System.IO.Path.GetDirectoryName(relative);
        if (string.IsNullOrEmpty(directory))
        {
            return root;
        }

        string[] segments = directory.Split(
            new[] { '/', '\\' },
            StringSplitOptions.RemoveEmptyEntries);

        var parts = new List<string>();
        if (root.Length > 0)
        {
            parts.Add(root);
        }

        for (var i = 0; i < segments.Length; i++)
        {
            string segment = FnvHash.SanitizeIdentifier(segments[i]);
            if (segment.Length > 0)
            {
                parts.Add(segment);
            }
        }

        return string.Join(".", parts);
    }

    public static bool IsValidNamespace(string value)
    {
        if (value.Length == 0)
        {
            return true;
        }

        string[] parts = value.Split('.');
        for (var i = 0; i < parts.Length; i++)
        {
            if (!IsValidIdentifier(parts[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsValidIdentifier(string value)
    {
        return value.Length > 0
               && SyntaxFacts.IsValidIdentifier(value)
               && SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None;
    }

    public static ITypeSymbol? GetEnumerableElementType(Compilation compilation, ITypeSymbol type)
    {
        type = TypeNames.UnwrapNullable(type);
        if (type is IArrayTypeSymbol array)
        {
            return array.ElementType;
        }

        if (type.SpecialType == SpecialType.System_String)
        {
            return compilation.GetSpecialType(SpecialType.System_Char);
        }

        if (type is INamedTypeSymbol named)
        {
            if (named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                && named.TypeArguments.Length == 1)
            {
                return named.TypeArguments[0];
            }

            for (var i = 0; i < named.AllInterfaces.Length; i++)
            {
                INamedTypeSymbol iface = named.AllInterfaces[i];
                if (iface.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                    && iface.TypeArguments.Length == 1)
                {
                    return iface.TypeArguments[0];
                }
            }
        }

        return null;
    }

    private static void CollectTypes(INamespaceOrTypeSymbol container, string name, List<INamedTypeSymbol> matches)
    {
        foreach (ISymbol member in container.GetMembers())
        {
            if (member is INamedTypeSymbol type)
            {
                if (string.Equals(type.Name, name, StringComparison.Ordinal))
                {
                    matches.Add(type);
                }

                CollectTypes(type, name, matches);
            }
            else if (member is INamespaceSymbol ns)
            {
                CollectTypes(ns, name, matches);
            }
        }
    }
}
