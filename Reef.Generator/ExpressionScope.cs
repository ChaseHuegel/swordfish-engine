using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Reef.Generator;

internal sealed class MarkupHelper
{
    public string Name = string.Empty;
    public ITypeSymbol ReturnType = null!;
    public readonly List<ITypeSymbol> Parameters = [];
    public readonly List<string> ParameterNames = [];
    public bool IsCallback;
    public Location Location = Location.None;
    public bool IsDeclared;

    public string SignatureKey()
    {
        var parts = new List<string> { Name };
        for (var i = 0; i < Parameters.Count; i++)
        {
            parts.Add(TypeNames.Display(Parameters[i]));
        }

        return string.Join("|", parts);
    }
}

internal sealed class ExpressionScope
{
    public Compilation Compilation = null!;
    public INamedTypeSymbol? CompanionType;
    public ITypeSymbol? ContextType;
    public ITypeSymbol? RendererDataType;
    public string ContextExpression = "context";
    public string BuilderExpression = "ui";
    public readonly Dictionary<string, ITypeSymbol> Variables = new(StringComparer.Ordinal);
    public readonly List<Diagnostic> Diagnostics;
    public readonly List<MarkupHelper> Helpers;
    public readonly HashSet<string> ExistingMethodKeys = new(StringComparer.Ordinal);
    public readonly HashSet<string> DeclaredHelperKeys = new(StringComparer.Ordinal);

    public ExpressionScope(
        Compilation compilation,
        INamedTypeSymbol? companionType,
        ITypeSymbol? contextType,
        ITypeSymbol? rendererDataType,
        string contextExpression,
        string builderExpression,
        Dictionary<string, ITypeSymbol> variables,
        List<Diagnostic> diagnostics,
        List<MarkupHelper> helpers)
    {
        Compilation = compilation;
        CompanionType = companionType;
        ContextType = contextType;
        RendererDataType = rendererDataType;
        ContextExpression = contextExpression;
        BuilderExpression = builderExpression;
        Diagnostics = diagnostics;
        Helpers = helpers;

        foreach (KeyValuePair<string, ITypeSymbol> pair in variables)
        {
            Variables[pair.Key] = pair.Value;
        }

        ITypeSymbol? uiType = TypeNames.GetUIBuilderType(compilation, rendererDataType);
        if (uiType != null)
        {
            Variables[builderExpression] = uiType;
        }

        if (contextType != null && contextExpression.Length > 0)
        {
            string root = GetRootIdentifier(contextExpression);
            if (root.Length > 0 && string.Equals(root, contextExpression.Trim(), StringComparison.Ordinal))
            {
                Variables[root] = contextType;
            }
        }

        if (companionType != null)
        {
            foreach (ISymbol member in companionType.GetMembers())
            {
                if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                {
                    ExistingMethodKeys.Add(BuildExistingMethodKey(method));
                }
            }
        }
    }

    public ExpressionScope CloneWith(string contextExpression, string builderExpression, ITypeSymbol? contextType)
    {
        var scope = new ExpressionScope(
            Compilation,
            CompanionType,
            contextType,
            RendererDataType,
            contextExpression,
            builderExpression,
            Variables,
            Diagnostics,
            Helpers);
        return scope;
    }

    public MarkupHelper? FindHelper(string name, List<ITypeSymbol> parameters)
    {
        for (var i = 0; i < Helpers.Count; i++)
        {
            MarkupHelper helper = Helpers[i];
            if (!string.Equals(helper.Name, name, StringComparison.Ordinal) || helper.Parameters.Count != parameters.Count)
            {
                continue;
            }

            var matches = true;
            for (var j = 0; j < parameters.Count; j++)
            {
                if (!TypeNames.IsSameType(helper.Parameters[j], parameters[j]))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return helper;
            }
        }

        return null;
    }

    public IMethodSymbol? FindCompanionMethod(string name, int argumentCount)
    {
        if (CompanionType == null)
        {
            return null;
        }

        foreach (ISymbol member in CompanionType.GetMembers(name))
        {
            if (member is IMethodSymbol method && method.Parameters.Length == argumentCount)
            {
                return method;
            }
        }

        return null;
    }

    public IMethodSymbol? FindCompanionMethod(string name)
    {
        if (CompanionType == null)
        {
            return null;
        }

        foreach (ISymbol member in CompanionType.GetMembers(name))
        {
            if (member is IMethodSymbol method)
            {
                return method;
            }
        }

        return null;
    }

    public ISymbol? FindCompanionMember(string name)
    {
        if (CompanionType == null)
        {
            return null;
        }

        foreach (ISymbol member in CompanionType.GetMembers(name))
        {
            if (member is IPropertySymbol || member is IFieldSymbol || member is IMethodSymbol)
            {
                return member;
            }
        }

        return null;
    }

    public static string GetRootIdentifier(string expression)
    {
        int index = 0;
        while (index < expression.Length && char.IsWhiteSpace(expression[index]))
        {
            index++;
        }

        int start = index;
        while (index < expression.Length && (char.IsLetterOrDigit(expression[index]) || expression[index] == '_'))
        {
            index++;
        }

        return index > start ? expression.Substring(start, index - start) : string.Empty;
    }

    private static string BuildExistingMethodKey(IMethodSymbol method)
    {
        var parts = new List<string> { method.Name };
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            parts.Add(TypeNames.Display(method.Parameters[i].Type));
        }

        return string.Join("|", parts);
    }
}

internal static class CompanionMethods
{
    public static bool IsPartialImplementation(IMethodSymbol method)
    {
        foreach (SyntaxReference reference in method.DeclaringSyntaxReferences)
        {
            SyntaxNode node = reference.GetSyntax();
            if (node is not Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax declaration)
            {
                continue;
            }

            bool partial = false;
            foreach (SyntaxToken modifier in declaration.Modifiers)
            {
                if (modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
                {
                    partial = true;
                    break;
                }
            }

            if (partial && (declaration.Body != null || declaration.ExpressionBody != null))
            {
                return true;
            }
        }

        return false;
    }
}

internal static class TypeNames
{
    private static readonly SymbolDisplayFormat DISPLAY_FORMAT = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static string Display(ITypeSymbol? type)
    {
        return type == null ? "object" : type.ToDisplayString(DISPLAY_FORMAT);
    }

    public static string DisplayForSource(ITypeSymbol type)
    {
        return type.ToDisplayString(DISPLAY_FORMAT);
    }

    public static INamedTypeSymbol? GetUIBuilderType(Compilation compilation, ITypeSymbol? rendererDataType)
    {
        INamedTypeSymbol? definition = compilation.GetTypeByMetadataName("Reef.UIBuilder`1");
        if (definition == null)
        {
            return null;
        }

        ITypeSymbol argument = rendererDataType ?? compilation.ObjectType;
        return definition.Construct(argument);
    }

    public static ITypeSymbol? GetUIControllerKeyType(Compilation compilation)
    {
        INamedTypeSymbol? controller = compilation.GetTypeByMetadataName("Reef.UIController");
        if (controller == null)
        {
            return null;
        }

        foreach (INamedTypeSymbol nested in controller.GetTypeMembers("Key"))
        {
            return nested;
        }

        return null;
    }

    public static ITypeSymbol? GetVector4Type(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName("System.Numerics.Vector4");
    }

    public static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            return named.TypeArguments[0];
        }

        return type;
    }

    public static bool IsSameType(ITypeSymbol left, ITypeSymbol right)
    {
        return SymbolEqualityComparer.Default.Equals(left, right);
    }

    public static bool IsBool(ITypeSymbol type)
    {
        return UnwrapNullable(type).SpecialType == SpecialType.System_Boolean;
    }

    public static bool IsString(ITypeSymbol type)
    {
        return UnwrapNullable(type).SpecialType == SpecialType.System_String;
    }

    public static bool IsNumeric(ITypeSymbol type)
    {
        switch (UnwrapNullable(type).SpecialType)
        {
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return true;
            default:
                return false;
        }
    }

    public static bool IsInteger(ITypeSymbol type)
    {
        switch (UnwrapNullable(type).SpecialType)
        {
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                return true;
            default:
                return false;
        }
    }
}
