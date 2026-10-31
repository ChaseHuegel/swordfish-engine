using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Reef.Generator;

internal sealed class BoundExpression
{
    public string Code = string.Empty;
    public ITypeSymbol? Type;
    public bool IsValid;
    public bool IsMethodGroup;
    public ISymbol? Symbol;
}

internal sealed class BoundCallback
{
    public string Code = string.Empty;
    public bool IsValid;
}

internal sealed class BoundBindTarget
{
    public string Code = string.Empty;
    public ITypeSymbol? Type;
    public bool IsValid;
    public string MemberName = string.Empty;
    public bool IsStructMember;
}

internal sealed class ExpressionBinder
{
    private readonly ExpressionScope _scope;

    public ExpressionBinder(ExpressionScope scope)
    {
        _scope = scope;
    }

    public bool TryBind(string raw, ITypeSymbol? expectedType, Location location, out BoundExpression bonus)
    {
        bonus = new BoundExpression();
        if (!TryNormalize(raw, location, out string normalized))
        {
            return false;
        }

        ExpressionSyntax syntax = SyntaxFactory.ParseExpression(normalized);
        if (syntax.ContainsDiagnostics)
        {
            AddDiagnostic(
                DiagnosticDescriptors.InvalidExpression,
                location,
                raw,
                "The expression is not valid C#.");
            return false;
        }

        BoundExpression bound = BindNode(syntax, expectedType, location);
        bonus = bound;
        if (!bound.IsValid)
        {
            return false;
        }

        if (expectedType != null && bound.Type != null && !IsCompatible(bound.Type, expectedType))
        {
            AddDiagnostic(
                DiagnosticDescriptors.TypeMismatch,
                location,
                "Expected '" + TypeNames.Display(expectedType) + "' but found '" + TypeNames.Display(bound.Type) + "'.");
            return false;
        }

        return true;
    }

    public bool TryBindCallback(string raw, Location location, out BoundCallback callback)
    {
        callback = new BoundCallback();
        string trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            AddDiagnostic(DiagnosticDescriptors.InvalidExpression, location, raw, "The callback expression is empty.");
            return false;
        }

        if (!trimmed.Contains("(") && TryBindDelegateCallback(trimmed, location, out callback))
        {
            return true;
        }

        if (!trimmed.Contains("("))
        {
            trimmed += "()";
        }

        if (!TryNormalize(trimmed, location, out string normalized))
        {
            return false;
        }

        ExpressionSyntax syntax = SyntaxFactory.ParseExpression(normalized);
        if (syntax.ContainsDiagnostics || syntax is not InvocationExpressionSyntax invocation)
        {
            AddDiagnostic(
                DiagnosticDescriptors.InvalidExpression,
                location,
                raw,
                "The callback must be a method name or a method call.");
            return false;
        }

        BoundExpression bound = BindInvocation(invocation, null, location, callback: true);
        if (!bound.IsValid)
        {
            return false;
        }

        callback.Code = bound.Code;
        callback.IsValid = true;
        return true;
    }

    private bool TryBindDelegateCallback(string raw, Location location, out BoundCallback callback)
    {
        callback = new BoundCallback();
        if (!TryNormalize(raw, location, out string normalized))
        {
            return false;
        }

        ExpressionSyntax syntax = SyntaxFactory.ParseExpression(normalized);
        if (syntax.ContainsDiagnostics)
        {
            return false;
        }

        BoundExpression bound = BindNode(syntax, null, location);
        if (!bound.IsValid || bound.IsMethodGroup || bound.Type is not INamedTypeSymbol named)
        {
            return false;
        }

        if (named.DelegateInvokeMethod == null)
        {
            return false;
        }

        callback.Code = bound.Code + "?.Invoke()";
        callback.IsValid = true;
        return true;
    }

    public bool TryBindBindTarget(string raw, Location location, out BoundBindTarget target)
    {
        target = new BoundBindTarget();
        string trimmed = raw.Trim();
        if (trimmed.StartsWith("@", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(1);
        }

        if (!TryNormalize(trimmed, location, out string normalized))
        {
            return false;
        }

        ExpressionSyntax syntax = SyntaxFactory.ParseExpression(normalized);
        if (syntax.ContainsDiagnostics)
        {
            AddDiagnostic(DiagnosticDescriptors.InvalidBind, location, "The bind target is not a valid expression.");
            return false;
        }

        BoundExpression bound = BindNode(syntax, null, location);
        if (!bound.IsValid || bound.Type == null)
        {
            AddDiagnostic(DiagnosticDescriptors.InvalidBind, location, "The bind target cannot be resolved.");
            return false;
        }

        if (!TryGetBindSymbol(syntax, out ISymbol? symbol) || symbol == null)
        {
            AddDiagnostic(DiagnosticDescriptors.InvalidBind, location, "The bind target must be a property or field.");
            return false;
        }

        bool settable = symbol switch
        {
            IPropertySymbol property => property.SetMethod != null && !property.IsReadOnly,
            IFieldSymbol field => !field.IsReadOnly && !field.IsConst,
            _ => false,
        };

        if (!settable)
        {
            AddDiagnostic(DiagnosticDescriptors.InvalidBind, location, "The bind target is read-only.");
            return false;
        }

        target.Code = bound.Code;
        target.Type = bound.Type;
        target.MemberName = symbol.Name;
        target.IsStructMember = bound.Type.IsValueType;
        target.IsValid = true;
        return true;
    }

    public bool TryBindHotkey(string raw, Location location, out BoundExpression bound)
    {
        bound = new BoundExpression();
        string trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            AddDiagnostic(DiagnosticDescriptors.InvalidExpression, location, raw, "The hotkey expression is empty.");
            return false;
        }

        ITypeSymbol? keyType = TypeNames.GetUIControllerKeyType(_scope.Compilation);
        if (keyType == null)
        {
            AddDiagnostic(
                DiagnosticDescriptors.UnresolvedExpression,
                location,
                "Reef.UIController.Key was not found in the compilation.");
            return false;
        }

        if (!trimmed.StartsWith("@", StringComparison.Ordinal)
            && trimmed.IndexOf('.') < 0
            && trimmed.IndexOf('(') < 0)
        {
            if (!HasEnumMember(keyType, trimmed))
            {
                AddDiagnostic(
                    DiagnosticDescriptors.InvalidEnumValue,
                    location,
                    trimmed,
                    "Reef.UIController.Key");
                return false;
            }

            bound.Code = TypeNames.DisplayForSource(keyType) + "." + trimmed;
            bound.Type = keyType;
            bound.IsValid = true;
            return true;
        }

        if (!TryBind(raw, keyType, location, out BoundExpression expression))
        {
            return false;
        }

        if (expression.Type == null || !TypeNames.IsSameType(TypeNames.UnwrapNullable(expression.Type), keyType))
        {
            AddDiagnostic(
                DiagnosticDescriptors.TypeMismatch,
                location,
                "The hotkey expression must return Reef.UIController.Key.");
            return false;
        }

        bound = expression;
        return true;
    }

    private BoundExpression BindNode(ExpressionSyntax syntax, ITypeSymbol? expectedType, Location location)
    {
        switch (syntax)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return BindNode(parenthesized.Expression, expectedType, location);
            case IdentifierNameSyntax identifier:
                return BindIdentifier(identifier, location);
            case MemberAccessExpressionSyntax memberAccess:
                return BindMemberAccess(memberAccess, location);
            case InvocationExpressionSyntax invocation:
                return BindInvocation(invocation, expectedType, location, callback: false);
            case ElementAccessExpressionSyntax elementAccess:
                return BindElementAccess(elementAccess, location);
            case LiteralExpressionSyntax literal:
                return BindLiteral(literal);
            case PrefixUnaryExpressionSyntax prefix:
                return BindPrefix(prefix, location);
            case BinaryExpressionSyntax binary:
                return BindBinary(binary, location);
            case InterpolatedStringExpressionSyntax interpolated:
                return BindInterpolated(interpolated, location);
            case ConditionalExpressionSyntax conditional:
                return BindConditional(conditional, location);
            case ThisExpressionSyntax:
                return _scope.CompanionType == null
                    ? Invalid("There is no companion class in this compilation.", location)
                    : new BoundExpression
                    {
                        Code = syntax.ToFullString().Trim(),
                        Type = _scope.CompanionType,
                        IsValid = true,
                    };
            default:
                return Invalid(
                    "Expression form '" + syntax.Kind() + "' is not supported.",
                    location);
        }
    }

    private BoundExpression BindIdentifier(IdentifierNameSyntax identifier, Location location)
    {
        string name = identifier.Identifier.ValueText;
        if (_scope.Variables.TryGetValue(name, out ITypeSymbol? type))
        {
            return new BoundExpression
            {
                Code = string.Equals(name, "param", StringComparison.Ordinal)
                    ? _scope.ParameterName
                    : identifier.ToFullString().Trim(),
                Type = type,
                IsValid = true,
            };
        }

        if (string.Equals(name, "true", StringComparison.Ordinal)
            || string.Equals(name, "false", StringComparison.Ordinal))
        {
            return new BoundExpression
            {
                Code = name,
                Type = _scope.Compilation.GetSpecialType(SpecialType.System_Boolean),
                IsValid = true,
            };
        }

        if (string.Equals(name, "null", StringComparison.Ordinal))
        {
            return new BoundExpression
            {
                Code = "null",
                Type = null,
                IsValid = true,
            };
        }

        ISymbol? companionMember = _scope.FindCompanionMember(name);
        if (companionMember is IPropertySymbol property)
        {
            return new BoundExpression
            {
                Code = identifier.ToFullString().Trim(),
                Type = property.Type,
                IsValid = true,
                Symbol = property,
            };
        }

        if (companionMember is IFieldSymbol field)
        {
            return new BoundExpression
            {
                Code = identifier.ToFullString().Trim(),
                Type = field.Type,
                IsValid = true,
                Symbol = field,
            };
        }

        if (companionMember is IMethodSymbol method)
        {
            return new BoundExpression
            {
                Code = identifier.ToFullString().Trim(),
                Type = method.ReturnType,
                IsValid = true,
                IsMethodGroup = true,
                Symbol = method,
            };
        }

        return Invalid("Identifier '" + name + "' cannot be resolved.", location);
    }

    private BoundExpression BindMemberAccess(MemberAccessExpressionSyntax memberAccess, Location location)
    {
        string name = memberAccess.Name.Identifier.ValueText;
        BoundExpression left = BindNode(memberAccess.Expression, null, location);
        if (!left.IsValid || left.Type == null)
        {
            return Invalid("The left side of '" + name + "' cannot be resolved.", location);
        }

        if (TryResolveMember(left.Type, name, out ISymbol? symbol, out ITypeSymbol? memberType))
        {
            string expressionText = memberAccess.Expression.ToFullString().Trim();
            string fullText = memberAccess.ToFullString().Trim();
            string code = string.Equals(expressionText, left.Code, StringComparison.Ordinal)
                ? fullText
                : left.Code + fullText.Substring(expressionText.Length);
            return new BoundExpression
            {
                Code = code,
                Type = memberType,
                IsValid = true,
                IsMethodGroup = symbol is IMethodSymbol,
                Symbol = symbol,
            };
        }

        return Invalid(
            "Member '" + name + "' does not exist on '" + TypeNames.Display(left.Type) + "'.",
            location);
    }

    private BoundExpression BindInvocation(
        InvocationExpressionSyntax invocation,
        ITypeSymbol? expectedType,
        Location location,
        bool callback)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            string name = memberAccess.Name.Identifier.ValueText;
            BoundExpression left = BindNode(memberAccess.Expression, null, location);
            if (!left.IsValid || left.Type == null)
            {
                return Invalid("The invoked receiver '" + name + "' cannot be resolved.", location);
            }

            var arguments = new List<ITypeSymbol?>();
            for (var i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
            {
                BoundExpression arg = BindNode(invocation.ArgumentList.Arguments[i].Expression, null, location);
                if (!arg.IsValid)
                {
                    return Invalid("An argument to '" + name + "' cannot be resolved.", location);
                }

                arguments.Add(arg.Type);
            }

            if (TryResolveMember(left.Type, name, out ISymbol? delegateMember, out ITypeSymbol? delegateType)
                && delegateMember is IPropertySymbol or IFieldSymbol
                && delegateType is INamedTypeSymbol delegateNamed
                && delegateNamed.DelegateInvokeMethod is not null)
            {
                return new BoundExpression
                {
                    Code = left.Code + "." + name + "?.Invoke" + invocation.ArgumentList,
                    Type = delegateNamed.DelegateInvokeMethod.ReturnType,
                    IsValid = true,
                    IsMethodGroup = true,
                    Symbol = delegateMember,
                };
            }

            IMethodSymbol? method = ResolveMethod(left.Type, name, arguments);
            if (method == null)
            {
                return Invalid(
                    "Method '" + name + "' with " + arguments.Count + " argument(s) was not found on '"
                    + TypeNames.Display(left.Type) + "'.",
                    location);
            }

            return new BoundExpression
            {
                Code = invocation.ToFullString().Trim(),
                Type = method.ReturnType,
                IsValid = true,
                IsMethodGroup = true,
                Symbol = method,
            };
        }

        if (invocation.Expression is IdentifierNameSyntax identifier)
        {
            string name = identifier.Identifier.ValueText;
            var argumentTypes = new List<ITypeSymbol>();
            for (var i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
            {
                BoundExpression arg = BindNode(invocation.ArgumentList.Arguments[i].Expression, null, location);
                if (!arg.IsValid || arg.Type == null)
                {
                    return Invalid("An argument to helper '" + name + "' cannot be resolved.", location);
                }

                argumentTypes.Add(arg.Type);
            }

            IMethodSymbol? companion = _scope.FindCompanionMethod(name, argumentTypes.Count);
            if (companion != null && !CompanionMethods.IsPartialImplementation(companion))
            {
                return new BoundExpression
                {
                    Code = invocation.ToFullString().Trim(),
                    Type = companion.ReturnType,
                    IsValid = true,
                    IsMethodGroup = true,
                    Symbol = companion,
                };
            }

            if (companion != null && CompanionMethods.IsPartialImplementation(companion))
            {
                //  The companion has an implementing declaration but no
                //  defining declaration. Emit the definition so the pair
                //  compiles.
                MarkupHelper partialHelper;
                if (callback)
                {
                    partialHelper = GetOrCreateHelper(
                        name,
                        _scope.Compilation.GetSpecialType(SpecialType.System_Void),
                        argumentTypes,
                        isCallback: true,
                        location);
                }
                else
                {
                    partialHelper = GetOrCreateHelper(name, companion.ReturnType, argumentTypes, isCallback: false, location);
                }

                if (partialHelper.ParameterNames.Count == 0)
                {
                    for (var i = 0; i < companion.Parameters.Length; i++)
                    {
                        partialHelper.ParameterNames.Add(companion.Parameters[i].Name);
                    }
                }

                return new BoundExpression
                {
                    Code = invocation.ToFullString().Trim(),
                    Type = companion.ReturnType,
                    IsValid = true,
                    IsMethodGroup = true,
                    Symbol = companion,
                };
            }

            if (callback)
            {
                MarkupHelper helper = GetOrCreateHelper(
                    name,
                    _scope.Compilation.GetSpecialType(SpecialType.System_Void),
                    argumentTypes,
                    isCallback: true,
                    location);
                return new BoundExpression
                {
                    Code = invocation.ToFullString().Trim(),
                    Type = helper.ReturnType,
                    IsValid = true,
                    IsMethodGroup = true,
                };
            }

            if (expectedType == null)
            {
                return Invalid(
                    "Helper method '" + name + "' has no companion implementation, so its return type cannot be inferred.",
                    location);
            }

            MarkupHelper created = GetOrCreateHelper(name, expectedType, argumentTypes, isCallback: false, location);
            return new BoundExpression
            {
                Code = invocation.ToFullString().Trim(),
                Type = created.ReturnType,
                IsValid = true,
                IsMethodGroup = true,
            };
        }

        return Invalid("Unsupported invocation target.", location);
    }

    private BoundExpression BindElementAccess(ElementAccessExpressionSyntax elementAccess, Location location)
    {
        BoundExpression left = BindNode(elementAccess.Expression, null, location);
        if (!left.IsValid || left.Type == null)
        {
            return Invalid("The indexed expression cannot be resolved.", location);
        }

        for (var i = 0; i < elementAccess.ArgumentList.Arguments.Count; i++)
        {
            BoundExpression arg = BindNode(elementAccess.ArgumentList.Arguments[i].Expression, null, location);
            if (!arg.IsValid)
            {
                return Invalid("An indexer argument cannot be resolved.", location);
            }
        }

        ITypeSymbol? elementType = ResolveIndexer(left.Type);
        if (elementType == null)
        {
            return Invalid("Type '" + TypeNames.Display(left.Type) + "' has no indexer.", location);
        }

        return new BoundExpression
        {
            Code = elementAccess.ToFullString().Trim(),
            Type = elementType,
            IsValid = true,
        };
    }

    private BoundExpression BindLiteral(LiteralExpressionSyntax literal)
    {
        ITypeSymbol type;
        switch (literal.Kind())
        {
            case SyntaxKind.StringLiteralExpression:
                type = _scope.Compilation.GetSpecialType(SpecialType.System_String);
                break;
            case SyntaxKind.CharacterLiteralExpression:
                type = _scope.Compilation.GetSpecialType(SpecialType.System_Char);
                break;
            case SyntaxKind.TrueLiteralExpression:
            case SyntaxKind.FalseLiteralExpression:
                type = _scope.Compilation.GetSpecialType(SpecialType.System_Boolean);
                break;
            case SyntaxKind.NullLiteralExpression:
                return new BoundExpression
                {
                    Code = literal.ToFullString().Trim(),
                    Type = null,
                    IsValid = true,
                };
            case SyntaxKind.NumericLiteralExpression:
                string text = literal.Token.Text;
                if (text.IndexOf('.') >= 0 || text.EndsWith("d", StringComparison.OrdinalIgnoreCase)
                    || text.EndsWith("f", StringComparison.OrdinalIgnoreCase) || text.EndsWith("m", StringComparison.OrdinalIgnoreCase))
                {
                    type = _scope.Compilation.GetSpecialType(SpecialType.System_Double);
                }
                else
                {
                    type = _scope.Compilation.GetSpecialType(SpecialType.System_Int32);
                }

                break;
            default:
                type = _scope.Compilation.GetSpecialType(SpecialType.System_Object);
                break;
        }

        return new BoundExpression
        {
            Code = literal.ToFullString().Trim(),
            Type = type,
            IsValid = true,
        };
    }

    private BoundExpression BindPrefix(PrefixUnaryExpressionSyntax prefix, Location location)
    {
        BoundExpression operand = BindNode(prefix.Operand, null, location);
        if (!operand.IsValid || operand.Type == null)
        {
            return Invalid("The unary expression cannot be resolved.", location);
        }

        if (prefix.Kind() == SyntaxKind.LogicalNotExpression)
        {
            return new BoundExpression
            {
                Code = prefix.ToFullString().Trim(),
                Type = _scope.Compilation.GetSpecialType(SpecialType.System_Boolean),
                IsValid = true,
            };
        }

        return new BoundExpression
        {
            Code = prefix.ToFullString().Trim(),
            Type = operand.Type,
            IsValid = true,
        };
    }

    private BoundExpression BindBinary(BinaryExpressionSyntax binary, Location location)
    {
        BoundExpression left = BindNode(binary.Left, null, location);
        BoundExpression right = BindNode(binary.Right, null, location);
        if (!left.IsValid || !right.IsValid)
        {
            return Invalid("The binary expression cannot be resolved.", location);
        }

        switch (binary.Kind())
        {
            case SyntaxKind.EqualsExpression:
            case SyntaxKind.NotEqualsExpression:
            case SyntaxKind.LessThanExpression:
            case SyntaxKind.LessThanOrEqualExpression:
            case SyntaxKind.GreaterThanExpression:
            case SyntaxKind.GreaterThanOrEqualExpression:
            case SyntaxKind.LogicalAndExpression:
            case SyntaxKind.LogicalOrExpression:
                return new BoundExpression
                {
                    Code = binary.ToFullString().Trim(),
                    Type = _scope.Compilation.GetSpecialType(SpecialType.System_Boolean),
                    IsValid = true,
                };
            case SyntaxKind.AddExpression:
                if ((left.Type != null && TypeNames.IsString(left.Type))
                    || (right.Type != null && TypeNames.IsString(right.Type)))
                {
                    return new BoundExpression
                    {
                        Code = binary.ToFullString().Trim(),
                        Type = _scope.Compilation.GetSpecialType(SpecialType.System_String),
                        IsValid = true,
                    };
                }

                break;
        }

        ITypeSymbol? type = left.Type ?? right.Type;
        if (type != null && TypeNames.IsNumeric(type))
        {
            return new BoundExpression
            {
                Code = binary.ToFullString().Trim(),
                Type = type,
                IsValid = true,
            };
        }

        return Invalid("The binary expression has no supported result type.", location);
    }

    private BoundExpression BindInterpolated(InterpolatedStringExpressionSyntax interpolated, Location location)
    {
        for (var i = 0; i < interpolated.Contents.Count; i++)
        {
            if (interpolated.Contents[i] is InterpolationSyntax interpolation)
            {
                BoundExpression expression = BindNode(interpolation.Expression, null, location);
                if (!expression.IsValid)
                {
                    return Invalid("An interpolated expression cannot be resolved.", location);
                }
            }
        }

        return new BoundExpression
        {
            Code = interpolated.ToFullString().Trim(),
            Type = _scope.Compilation.GetSpecialType(SpecialType.System_String),
            IsValid = true,
        };
    }

    private BoundExpression BindConditional(ConditionalExpressionSyntax conditional, Location location)
    {
        BoundExpression condition = BindNode(conditional.Condition, null, location);
        BoundExpression whenTrue = BindNode(conditional.WhenTrue, null, location);
        BoundExpression whenFalse = BindNode(conditional.WhenFalse, null, location);
        if (!condition.IsValid || !whenTrue.IsValid || !whenFalse.IsValid)
        {
            return Invalid("The conditional expression cannot be resolved.", location);
        }

        return new BoundExpression
        {
            Code = conditional.ToFullString().Trim(),
            Type = whenTrue.Type ?? whenFalse.Type,
            IsValid = true,
        };
    }

    private bool TryNormalize(string raw, Location location, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            AddDiagnostic(DiagnosticDescriptors.InvalidExpression, location, raw, "The expression is empty.");
            return false;
        }

        string text = raw.Trim();
        text = text.Replace("@context.", _scope.ContextExpression + ".");
        text = text.Replace("@Context.", _scope.ContextExpression + ".");
        text = text.Replace("@ui.", _scope.BuilderExpression + ".");
        text = text.Replace("@Context", _scope.ContextExpression);
        text = text.Replace("@context", _scope.ContextExpression);
        text = text.Replace("@ui", _scope.BuilderExpression);

        if (text.StartsWith("@", StringComparison.Ordinal))
        {
            text = text.Substring(1);
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'')
            {
                if (!AppendDoubleQuotedString(text, ref i, builder, location))
                {
                    return false;
                }

                continue;
            }

            builder.Append(c);
        }

        text = ReplaceOperatorAliases(builder.ToString());
        if (text.IndexOf('@') >= 0)
        {
            var cleaned = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '@' && i + 1 < text.Length && (char.IsLetter(text[i + 1]) || text[i + 1] == '_'))
                {
                    continue;
                }

                cleaned.Append(text[i]);
            }

            text = cleaned.ToString();
        }

        normalized = text;
        return true;
    }

    private bool AppendDoubleQuotedString(string text, ref int index, StringBuilder builder, Location location)
    {
        index++;
        builder.Append('"');
        while (index < text.Length)
        {
            char c = text[index];
            if (c == '\\' && index + 1 < text.Length)
            {
                char next = text[index + 1];
                if (next == '\'')
                {
                    builder.Append('\'');
                }
                else
                {
                    builder.Append(c);
                    builder.Append(next);
                }

                index += 2;
                continue;
            }

            if (c == '\'')
            {
                builder.Append('"');
                return true;
            }

            if (c == '"')
            {
                builder.Append("\\\"");
            }
            else if (c == '\r')
            {
                builder.Append("\\r");
            }
            else if (c == '\n')
            {
                builder.Append("\\n");
            }
            else
            {
                builder.Append(c);
            }

            index++;
        }

        AddDiagnostic(DiagnosticDescriptors.InvalidExpression, location, text, "The string literal is not terminated.");
        return false;
    }

    private static string ReplaceOperatorAliases(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        var index = 0;
        char previousSignificant = '\0';
        while (index < text.Length)
        {
            char c = text[index];
            if (c == '"')
            {
                int start = index;
                index++;
                while (index < text.Length)
                {
                    if (text[index] == '\\' && index + 1 < text.Length)
                    {
                        index += 2;
                        continue;
                    }

                    if (text[index] == '"')
                    {
                        index++;
                        break;
                    }

                    index++;
                }

                builder.Append(text, start, index - start);
                previousSignificant = '"';
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = index;
                while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_'))
                {
                    index++;
                }

                string word = text.Substring(start, index - start);
                bool replace = previousSignificant != '.'
                                && (index >= text.Length || !(char.IsLetterOrDigit(text[index]) || text[index] == '_'));
                if (replace)
                {
                    string? replacement = word switch
                    {
                        "lt" => "<",
                        "lte" => "<=",
                        "gt" => ">",
                        "gte" => ">=",
                        "eq" => "==",
                        "ne" => "!=",
                        "and" => "&&",
                        "or" => "||",
                        _ => null,
                    };
                    if (replacement != null)
                    {
                        builder.Append(' ');
                        builder.Append(replacement);
                        builder.Append(' ');
                        previousSignificant = replacement[replacement.Length - 1];
                        continue;
                    }
                }

                builder.Append(word);
                previousSignificant = word.Length > 0 ? word[word.Length - 1] : previousSignificant;
                continue;
            }

            builder.Append(c);
            if (!char.IsWhiteSpace(c))
            {
                if (char.IsLetterOrDigit(c) || c == ')' || c == ']')
                {
                    previousSignificant = c;
                }
                else
                {
                    previousSignificant = c;
                }
            }

            index++;
        }

        return builder.ToString();
    }

    private bool TryResolveMember(ITypeSymbol type, string name, out ISymbol? symbol, out ITypeSymbol? memberType)
    {
        symbol = null;
        memberType = null;
        type = TypeNames.UnwrapNullable(type);

        if (type is IArrayTypeSymbol array)
        {
            if (string.Equals(name, "Length", StringComparison.Ordinal))
            {
                symbol = null;
                memberType = _scope.Compilation.GetSpecialType(SpecialType.System_Int32);
                return true;
            }

            if (string.Equals(name, "LongLength", StringComparison.Ordinal))
            {
                memberType = _scope.Compilation.GetSpecialType(SpecialType.System_Int64);
                return true;
            }

            return false;
        }

        if (type is INamedTypeSymbol named)
        {
            for (INamedTypeSymbol? current = named; current != null; current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers(name))
                {
                    switch (member)
                    {
                        case IPropertySymbol property:
                            symbol = property;
                            memberType = property.Type;
                            return true;
                        case IFieldSymbol field:
                            symbol = field;
                            memberType = field.Type;
                            return true;
                        case IMethodSymbol method:
                            symbol = method;
                            memberType = method.ReturnType;
                            return true;
                    }
                }
            }

            for (var i = 0; i < named.AllInterfaces.Length; i++)
            {
                foreach (ISymbol member in named.AllInterfaces[i].GetMembers(name))
                {
                    if (member is IPropertySymbol property)
                    {
                        symbol = property;
                        memberType = property.Type;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private IMethodSymbol? ResolveMethod(ITypeSymbol type, string name, List<ITypeSymbol?> arguments)
    {
        type = TypeNames.UnwrapNullable(type);
        if (type is not INamedTypeSymbol named)
        {
            return null;
        }

        IMethodSymbol? fallback = null;
        for (INamedTypeSymbol? current = named; current != null; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(name))
            {
                if (member is not IMethodSymbol method || method.MethodKind != MethodKind.Ordinary)
                {
                    continue;
                }

                if (method.TypeParameters.Length > 0)
                {
                    continue;
                }

                int required = method.Parameters.Length;
                int optional = 0;
                for (var i = 0; i < method.Parameters.Length; i++)
                {
                    if (method.Parameters[i].IsOptional)
                    {
                        optional++;
                    }
                }

                if (arguments.Count > required || arguments.Count < required - optional)
                {
                    continue;
                }

                var compatible = true;
                for (var i = 0; i < arguments.Count; i++)
                {
                    ITypeSymbol? argument = arguments[i];
                    if (argument == null)
                    {
                        continue;
                    }

                    if (!IsCompatible(argument, method.Parameters[i].Type))
                    {
                        compatible = false;
                        break;
                    }
                }

                if (compatible)
                {
                    if (arguments.Count == required)
                    {
                        return method;
                    }

                    fallback ??= method;
                }
            }
        }

        return fallback;
    }

    private ITypeSymbol? ResolveIndexer(ITypeSymbol type)
    {
        type = TypeNames.UnwrapNullable(type);
        if (type is IArrayTypeSymbol array)
        {
            return array.ElementType;
        }

        if (type is INamedTypeSymbol named)
        {
            for (INamedTypeSymbol? current = named; current != null; current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers())
                {
                    if (member is IPropertySymbol property && property.IsIndexer)
                    {
                        return property.Type;
                    }
                }
            }
        }

        return null;
    }

    private MarkupHelper GetOrCreateHelper(
        string name,
        ITypeSymbol returnType,
        List<ITypeSymbol> parameters,
        bool isCallback,
        Location location)
    {
        MarkupHelper? existing = _scope.FindHelper(name, parameters);
        if (existing != null)
        {
            if (!isCallback && !TypeNames.IsSameType(existing.ReturnType, returnType))
            {
                AddDiagnostic(
                    DiagnosticDescriptors.TypeMismatch,
                    location,
                    "Helper '" + name + "' is used with conflicting return types.");
            }

            return existing;
        }

        var helper = new MarkupHelper
        {
            Name = name,
            ReturnType = returnType,
            IsCallback = isCallback,
            Location = location,
        };
        helper.Parameters.AddRange(parameters);
        _scope.Helpers.Add(helper);
        return helper;
    }

    private bool TryGetBindSymbol(ExpressionSyntax syntax, out ISymbol? symbol)
    {
        symbol = null;
        if (syntax is IdentifierNameSyntax identifier)
        {
            symbol = _scope.FindCompanionMember(identifier.Identifier.ValueText);
            return symbol != null;
        }

        if (syntax is MemberAccessExpressionSyntax memberAccess)
        {
            BoundExpression left = BindNode(memberAccess.Expression, null, Location.None);
            if (!left.IsValid || left.Type == null)
            {
                return false;
            }

            return TryResolveMember(left.Type, memberAccess.Name.Identifier.ValueText, out symbol, out _);
        }

        if (syntax is ParenthesizedExpressionSyntax parenthesized)
        {
            return TryGetBindSymbol(parenthesized.Expression, out symbol);
        }

        return false;
    }

    private bool IsCompatible(ITypeSymbol from, ITypeSymbol to)
    {
        if (TypeNames.IsSameType(from, to))
        {
            return true;
        }

        if (from is ITypeSymbol && to.SpecialType == SpecialType.System_Object)
        {
            return true;
        }

        Conversion conversion = _scope.Compilation.ClassifyConversion(from, to);
        if (conversion.IsImplicit)
        {
            return true;
        }

        if (TypeNames.IsNumeric(from) && TypeNames.IsNumeric(to))
        {
            return true;
        }

        return false;
    }

    private bool HasEnumMember(ITypeSymbol type, string name)
    {
        ITypeSymbol unwrapped = TypeNames.UnwrapNullable(type);
        if (unwrapped is not INamedTypeSymbol named)
        {
            return false;
        }

        foreach (ISymbol member in named.GetMembers(name))
        {
            if (member is IFieldSymbol field && field.HasConstantValue)
            {
                return true;
            }
        }

        return false;
    }

    private BoundExpression Invalid(string message, Location location)
    {
        AddDiagnostic(DiagnosticDescriptors.UnresolvedExpression, location, message);
        return new BoundExpression { IsValid = false };
    }

    private void AddDiagnostic(DiagnosticDescriptor descriptor, Location location, params object[] args)
    {
        _scope.Diagnostics.Add(Diagnostic.Create(descriptor, location, args));
    }
}
