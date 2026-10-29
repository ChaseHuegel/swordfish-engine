using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Reef.Generator;

internal sealed partial class DocumentState
{
    private void EmitExternalWidget(RmlElementNode node, EmitContext context)
    {
        if (!_document.NamespacePrefixes.TryGetValue(node.Prefix, out string? namespaceName))
        {
            Report(
                DiagnosticDescriptors.UnresolvedNamespace,
                node.Location,
                node.Prefix,
                "The prefix is not declared on the Reef root.");
            return;
        }

        INamespaceSymbol? namespaceSymbol = WidgetResolver.FindNamespace(Compilation, namespaceName);
        if (namespaceSymbol == null)
        {
            Report(
                DiagnosticDescriptors.UnresolvedNamespace,
                node.Location,
                node.Prefix,
                "The namespace '" + namespaceName + "' was not found.");
            return;
        }

        List<WidgetMethod> widgets = WidgetResolver.FindWidgets(Compilation, namespaceSymbol, node.Tag);
        if (widgets.Count == 0)
        {
            Report(DiagnosticDescriptors.UnknownElement, node.Location, node.Prefix + ":" + node.Tag);
            return;
        }

        WidgetMethod? selected = SelectWidget(widgets, node);
        if (selected == null)
        {
            Report(DiagnosticDescriptors.UnknownElement, node.Location, node.Prefix + ":" + node.Tag);
            return;
        }

        EmitWidgetCall(node, selected, context);
    }

    private WidgetMethod? SelectWidget(List<WidgetMethod> widgets, RmlElementNode node)
    {
        WidgetMethod? best = null;
        var bestScore = int.MinValue;
        for (var i = 0; i < widgets.Count; i++)
        {
            WidgetMethod widget = widgets[i];
            if (!WidgetFacts.IsApplicable(RendererType, widget.Method))
            {
                continue;
            }

            int score = 0;
            bool valid = true;
            for (var j = 1; j < widget.Method.Parameters.Length; j++)
            {
                IMethodSymbol method = widget.Method;
                IParameterSymbol parameter = method.Parameters[j];
                if (parameter.Type is INamedTypeSymbol named
                    && (named.ToDisplayString() == "Reef.UI.Style"
                        || named.ToDisplayString() == "Reef.UI.Constraints"
                        || named.ToDisplayString() == "Reef.UI.FontOptions"))
                {
                    score += 2;
                    continue;
                }

                if (parameter.RefKind == RefKind.Ref || parameter.RefKind == RefKind.Out)
                {
                    if (HasAttribute(node, "bind") == null)
                    {
                        valid = false;
                        break;
                    }

                    score += 2;
                    continue;
                }

                RmlAttribute? attribute = FindParameterAttribute(node, parameter.Name);
                if (attribute != null)
                {
                    score += 4;
                    continue;
                }

                if (!parameter.IsOptional && !parameter.HasExplicitDefaultValue)
                {
                    valid = false;
                    break;
                }

                score++;
            }

            if (!valid)
            {
                continue;
            }

            if (score > bestScore)
            {
                best = widget;
                bestScore = score;
            }
        }

        return best;
    }

    private void EmitWidgetCall(RmlElementNode node, WidgetMethod widget, EmitContext context)
    {
        IMethodSymbol method = widget.Method;
        ValidateWidgetAttributes(node, method);
        StylePlan plan = BuildStylePlan(node, context, out bool needsId);
        string? visibleCode = EmitBoolCondition(node, "visible", context, out bool visibleLiteral);
        if (visibleLiteral && string.Equals(visibleCode, "false", StringComparison.Ordinal))
        {
            return;
        }

        bool hasStyle = HasStyleAssignments(plan);
        bool hasConstraints = HasConstraintAssignments(plan);
        bool hasFont = HasFontAssignments(plan);
        bool hasLayout = HasLayoutAssignments(plan);

        bool hasStyleParameter = HasParameterType(method, "Reef.UI.Style");
        bool hasConstraintsParameter = HasParameterType(method, "Reef.UI.Constraints");
        bool hasFontParameter = HasParameterType(method, "Reef.UI.FontOptions");
        bool hasIdParameter = HasParameter(method, "id");

        RmlAttribute? idAttribute = HasAttribute(node, "id");
        RmlAttribute? bindAttribute = HasAttribute(node, "bind");
        RmlAttribute? valueAttribute = HasAttribute(node, "value");
        RmlAttribute? onchangeAttribute = HasAttribute(node, "onchange");

        bool needsWrapper = (hasStyle && !hasStyleParameter)
                            || (hasConstraints && !hasConstraintsParameter)
                            || (hasFont && !hasFontParameter)
                            || hasLayout
                            || (idAttribute != null && !hasIdParameter);

        if (idAttribute != null && !hasIdParameter)
        {
            needsId = true;
        }

        if (visibleCode != null)
        {
            context.Writer.Line("if (" + visibleCode + ")");
            context.Writer.Open();
        }

        if (needsWrapper)
        {
            if (idAttribute != null)
            {
                string? wrapperId = EmitStringExpression(idAttribute.Value, idAttribute.Location, context.Scope);
                context.Writer.Line(wrapperId == null
                    ? "using (" + context.Builder + ".Element())"
                    : "using (" + context.Builder + ".Element(" + wrapperId + "))");
            }
            else
            {
                context.Writer.Line("using (" + context.Builder + ".Element())");
            }
            context.Writer.Open();
            ApplyRuntimeCallerOverrides(node, context);
            ApplyStylePlan(node, plan, context);
        }
        else if (idAttribute != null && hasIdParameter)
        {
            needsId = true;
        }

        var styleName = needsWrapper || !hasStyleParameter || !hasStyle ? null : NextLocal("__style");
        var constraintsName = needsWrapper || !hasConstraintsParameter || !hasConstraints ? null : NextLocal("__constraints");
        var fontName = needsWrapper || !hasFontParameter || !hasFont ? null : NextLocal("__font");

        if (styleName != null)
        {
            context.Writer.Line("var " + styleName + " = new global::Reef.UI.Style();");
            ApplyAssignmentsToStyle(plan, node, context, styleName);
        }

        if (constraintsName != null)
        {
            context.Writer.Line("var " + constraintsName + " = new global::Reef.UI.Constraints();");
            ApplyAssignmentsToConstraints(plan, context, constraintsName);
        }

        if (fontName != null)
        {
            context.Writer.Line("var " + fontName + " = new global::Reef.UI.FontOptions { Size = 16 };");
            ApplyAssignmentsToFont(plan, context, fontName);
        }

        string call = BuildWidgetCallExpression(node, widget, context, styleName, constraintsName, fontName, bindAttribute);

        if (bindAttribute != null)
        {
            EmitWidgetBind(node, method, call, bindAttribute, context, needsWrapper);
        }
        else if (valueAttribute != null && onchangeAttribute != null)
        {
            EmitWidgetValueChange(node, method, call, valueAttribute, onchangeAttribute, context, needsWrapper);
        }
        else if (node.Children.Count > 0)
        {
            if (!WidgetFacts.IsDisposable(method.ReturnType))
            {
                Report(
                    DiagnosticDescriptors.WidgetChildren,
                    node.Location,
                    node.Prefix + ":" + node.Tag,
                    TypeNames.Display(method.ReturnType));
            }
            else
            {
                context.Writer.Line("using (" + call + ")");
                context.Writer.Open();
                EmitNodes(node.Children, context);
                context.Writer.Close();
            }
        }
        else
        {
            context.Writer.Line(call + ";");
        }

        if (needsWrapper)
        {
            context.Writer.Close();
        }

        if (visibleCode != null)
        {
            context.Writer.Close();
        }
    }

    private void ValidateWidgetAttributes(RmlElementNode node, IMethodSymbol method)
    {
        for (var i = 0; i < node.Attributes.Count; i++)
        {
            RmlAttribute attribute = node.Attributes[i];
            if (attribute.NamespaceUri == "http://www.w3.org/XML/1998/namespace"
                || attribute.NamespaceUri == "http://www.w3.org/2001/XMLSchema-instance"
                || attribute.IsClassDirective)
            {
                continue;
            }

            if (attribute.NamespaceUri.Length > 0)
            {
                Report(DiagnosticDescriptors.UnknownAttribute, attribute.Location, attribute.QualifiedName, node.Prefix + ":" + node.Tag, "Namespaced attributes are not valid on a widget.");
                continue;
            }

            if (string.Equals(attribute.Name, "class", StringComparison.Ordinal)
                || string.Equals(attribute.Name, "visible", StringComparison.Ordinal)
                || string.Equals(attribute.Name, "bind", StringComparison.Ordinal)
                || string.Equals(attribute.Name, "value", StringComparison.Ordinal)
                || string.Equals(attribute.Name, "onchange", StringComparison.Ordinal)
                || MarkupVocabulary.IsStyleAttribute(attribute.Name))
            {
                continue;
            }

            bool matched = false;
            for (var j = 1; j < method.Parameters.Length; j++)
            {
                if (WidgetResolver.NormalizeParameterName(method.Parameters[j].Name)
                    == WidgetResolver.NormalizeParameterName(attribute.Name))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                Report(
                    DiagnosticDescriptors.UnknownAttribute,
                    attribute.Location,
                    attribute.Name,
                    node.Prefix + ":" + node.Tag,
                    "The widget has no matching parameter.");
            }
        }
    }

    private string BuildWidgetCallExpression(
        RmlElementNode node,
        WidgetMethod widget,
        EmitContext context,
        string? styleName,
        string? constraintsName,
        string? fontName,
        RmlAttribute? bindAttribute)
    {
        IMethodSymbol method = widget.Method;
        var arguments = new List<string>
        {
            context.Builder,
        };

        for (var i = 1; i < method.Parameters.Length; i++)
        {
            IParameterSymbol parameter = method.Parameters[i];
            string parameterType = TypeNames.Display(parameter.Type);

            if (parameter.Type is INamedTypeSymbol named
                && named.ToDisplayString() == "Reef.UI.Style")
            {
                if (styleName != null)
                {
                    arguments.Add(parameter.Name + ": " + styleName);
                }
                else if (!parameter.IsOptional && !parameter.HasExplicitDefaultValue)
                {
                    arguments.Add(parameter.Name + ": default(global::Reef.UI.Style)");
                }

                continue;
            }

            if (parameter.Type is INamedTypeSymbol constraints
                && constraints.ToDisplayString() == "Reef.UI.Constraints")
            {
                if (constraintsName != null)
                {
                    arguments.Add(parameter.Name + ": " + constraintsName);
                }
                else if (!parameter.IsOptional && !parameter.HasExplicitDefaultValue)
                {
                    arguments.Add(parameter.Name + ": default(global::Reef.UI.Constraints)");
                }

                continue;
            }

            if (parameter.Type is INamedTypeSymbol fontOptions
                && fontOptions.ToDisplayString() == "Reef.UI.FontOptions")
            {
                if (fontName != null)
                {
                    arguments.Add(parameter.Name + ": " + fontName);
                }
                else if (!parameter.IsOptional && !parameter.HasExplicitDefaultValue)
                {
                    arguments.Add(parameter.Name + ": new global::Reef.UI.FontOptions { Size = 16 }");
                }

                continue;
            }

            if (parameter.RefKind == RefKind.Ref)
            {
                if (bindAttribute != null)
                {
                    var binder = new ExpressionBinder(context.Scope);
                    if (binder.TryBindBindTarget(bindAttribute.Value, bindAttribute.Location, out BoundBindTarget target))
                    {
                        if (target.Type != null && TypeNames.IsSameType(target.Type, parameter.Type))
                        {
                            arguments.Add(parameter.Name + ": ref " + target.Code);
                            continue;
                        }
                    }
                }

                Report(
                    DiagnosticDescriptors.InvalidBind,
                    bindAttribute?.Location ?? node.Location,
                    "A ref widget parameter must match the bind target type.");
                arguments.Add(parameter.Name + ": ref __invalidBindTarget");
                continue;
            }

            RmlAttribute? attribute = FindParameterAttribute(node, parameter.Name);
            if (attribute == null)
            {
                if (parameter.IsOptional || parameter.HasExplicitDefaultValue)
                {
                    continue;
                }

                Report(
                    DiagnosticDescriptors.MissingAttribute,
                    node.Location,
                    node.Prefix + ":" + node.Tag,
                    parameter.Name);
                continue;
            }

            string? value = ConvertWidgetArgument(node, attribute, parameter, context);
            if (value != null)
            {
                arguments.Add(parameter.Name + ": " + value);
            }
        }

        string containingType = TypeNames.Display(widget.ContainingType);
        return containingType + "." + method.Name + "(" + string.Join(", ", arguments) + ")";
    }

    private string? ConvertWidgetArgument(
        RmlElementNode node,
        RmlAttribute attribute,
        IParameterSymbol parameter,
        EmitContext context)
    {
        ITypeSymbol type = parameter.Type;
        if (type.SpecialType == SpecialType.System_String)
        {
            if (string.Equals(parameter.Name, "id", StringComparison.Ordinal))
            {
                return EmitStringExpression(attribute.Value, attribute.Location, context.Scope);
            }

            return EmitStringExpression(attribute.Value, attribute.Location, context.Scope);
        }

        if (type.SpecialType == SpecialType.System_Boolean)
        {
            bool? literal = EmitBoolAttribute(attribute, context);
            if (literal != null)
            {
                return literal.Value ? "true" : "false";
            }

            return EmitTypedExpression(attribute.Value, type, attribute.Location, context.Scope);
        }

        if (type.SpecialType == SpecialType.System_Int32
            || type.SpecialType == SpecialType.System_Int64
            || type.SpecialType == SpecialType.System_Single
            || type.SpecialType == SpecialType.System_Double
            || type.SpecialType == SpecialType.System_Decimal)
        {
            return EmitTypedExpression(attribute.Value, type, attribute.Location, context.Scope);
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return EmitEnumArgument(attribute, type, context);
        }

        if (TypeNames.Display(type) == "System.Numerics.Vector4")
        {
            if (attribute.Value.TrimStart().StartsWith("@", StringComparison.Ordinal))
            {
                return EmitTypedExpression(attribute.Value, type, attribute.Location, context.Scope);
            }

            if (ColorParser.TryParse(StyleValueParser.TokenizeValue(attribute.Value), out System.Numerics.Vector4 color, out _))
            {
                return FnvHash.FormatVector(color);
            }

            return null;
        }

        if (attribute.Value.TrimStart().StartsWith("@", StringComparison.Ordinal))
        {
            return EmitTypedExpression(attribute.Value, type, attribute.Location, context.Scope);
        }

        Report(
            DiagnosticDescriptors.TypeMismatch,
            attribute.Location,
            "Widget parameter '" + parameter.Name + "' of type '" + TypeNames.Display(type)
            + "' requires an expression.");
        return null;
    }

    private string? EmitEnumArgument(RmlAttribute attribute, ITypeSymbol type, EmitContext context)
    {
        string value = attribute.Value.Trim();
        if (value.StartsWith("@", StringComparison.Ordinal))
        {
            return EmitTypedExpression(value, type, attribute.Location, context.Scope);
        }

        string memberName = value;
        int dot = value.LastIndexOf('.');
        if (dot >= 0)
        {
            memberName = value.Substring(dot + 1);
        }

        if (type is INamedTypeSymbol named)
        {
            foreach (ISymbol member in named.GetMembers(memberName))
            {
                if (member is IFieldSymbol field && field.HasConstantValue)
                {
                    return TypeNames.Display(type) + "." + memberName;
                }
            }
        }

        Report(DiagnosticDescriptors.InvalidEnumValue, attribute.Location, value, TypeNames.Display(type));
        return null;
    }

    private void EmitWidgetBind(
        RmlElementNode node,
        IMethodSymbol method,
        string call,
        RmlAttribute bindAttribute,
        EmitContext context,
        bool wrapper)
    {
        var binder = new ExpressionBinder(context.Scope);
        if (!binder.TryBindBindTarget(bindAttribute.Value, bindAttribute.Location, out BoundBindTarget target) || target.Type == null)
        {
            return;
        }

        if (ContextType != null && ContextType.IsValueType)
        {
            Report(
                DiagnosticDescriptors.InvalidBind,
                bindAttribute.Location,
                "Bind requires a reference-type context so writeback is visible.");
            return;
        }

        IParameterSymbol? refParameter = null;
        for (var i = 1; i < method.Parameters.Length; i++)
        {
            IParameterSymbol parameter = method.Parameters[i];
            if (parameter.RefKind == RefKind.Ref && TypeNames.IsSameType(parameter.Type, target.Type))
            {
                refParameter = parameter;
                break;
            }
        }

        if (refParameter != null)
        {
            context.Writer.Line(call + ";");
            return;
        }

        string handler = "On" + target.MemberName + "Changed";
        if (WidgetFacts.TryGetControlResult(method.ReturnType, out ITypeSymbol? controlValue)
            && controlValue != null
            && TypeNames.IsSameType(controlValue, target.Type))
        {
            string resultName = NextLocal("__result");
            context.Writer.Line("var " + resultName + " = " + call + ";");
            context.Writer.Line("if (" + resultName + ".Changed)");
            context.Writer.Open();
            context.Writer.Line(target.Code + " = " + resultName + ".Value;");
            RegisterCallback(handler, target.Type, bindAttribute.Location);
            context.Writer.Line(handler + "(" + resultName + ".Value);");
            context.Writer.Close();
            return;
        }

        if (!IsAssignable(method.ReturnType, target.Type))
        {
            Report(
                DiagnosticDescriptors.InvalidBind,
                bindAttribute.Location,
                "The widget return type cannot be assigned to the bind target.");
            return;
        }

        string newName = NextLocal("__new");
        context.Writer.Line("var " + newName + " = " + call + ";");
        context.Writer.Line("if (!global::System.Object.Equals(" + newName + ", " + target.Code + "))");
        context.Writer.Open();
        context.Writer.Line(target.Code + " = " + newName + ";");
        RegisterCallback(handler, target.Type, bindAttribute.Location);
        context.Writer.Line(handler + "(" + newName + ");");
        context.Writer.Close();
    }

    private void EmitWidgetValueChange(
        RmlElementNode node,
        IMethodSymbol method,
        string call,
        RmlAttribute valueAttribute,
        RmlAttribute onchangeAttribute,
        EmitContext context,
        bool wrapper)
    {
        var binder = new ExpressionBinder(context.Scope);
        if (!binder.TryBind(valueAttribute.Value, null, valueAttribute.Location, out BoundExpression value) || value.Type == null)
        {
            return;
        }

        ITypeSymbol valueType = value.Type;
        if (WidgetFacts.TryGetControlResult(method.ReturnType, out ITypeSymbol? controlValue)
            && controlValue != null)
        {
            string resultName = NextLocal("__result");
            context.Writer.Line("var " + resultName + " = " + call + ";");
            context.Writer.Line("if (" + resultName + ".Changed)");
            context.Writer.Open();
            EmitOnChangeInvocation(onchangeAttribute, resultName + ".Value", valueType, context);
            context.Writer.Close();
            return;
        }

        if (IsAssignable(method.ReturnType, valueType))
        {
            string newName = NextLocal("__new");
            context.Writer.Line("var " + newName + " = " + call + ";");
            context.Writer.Line("if (!global::System.Object.Equals(" + newName + ", " + value.Code + "))");
            context.Writer.Open();
            EmitOnChangeInvocation(onchangeAttribute, newName, valueType, context);
            context.Writer.Close();
        }
    }

    private void EmitOnChangeInvocation(RmlAttribute onchange, string valueCode, ITypeSymbol valueType, EmitContext context)
    {
        string name = onchange.Value.Trim();
        if (!TypeResolver.IsValidIdentifier(name))
        {
            var binder = new ExpressionBinder(context.Scope);
            if (!binder.TryBindCallback(onchange.Value, onchange.Location, out BoundCallback callback))
            {
                return;
            }

            context.Writer.Line(callback.Code + ";");
            return;
        }

        RegisterCallback(name, valueType, onchange.Location);
        context.Writer.Line(name + "(" + valueCode + ");");
    }

    private void ApplyAssignmentsToStyle(StylePlan plan, RmlElementNode node, EmitContext context, string styleName)
    {
        ApplyStyleAssignments(plan.Normal, RssPseudoState.None, node, context, styleName);
        ApplyStyleAssignments(plan.Inline, RssPseudoState.None, node, context, styleName);
        ApplyStyleAssignments(plan.Final, RssPseudoState.None, node, context, styleName);
    }

    private void ApplyStyleAssignments(
        List<StyleLayer> layers,
        RssPseudoState state,
        RmlElementNode node,
        EmitContext context,
        string styleName)
    {
        for (var i = 0; i < layers.Count; i++)
        {
            StyleLayer layer = layers[i];
            if (layer.State != state || layer.Assignments.Count == 0)
            {
                continue;
            }

            if (layer.ConditionRaw != null)
            {
                string? condition = EmitTypedExpression(
                    layer.ConditionRaw,
                    Compilation.GetSpecialType(SpecialType.System_Boolean),
                    layer.Location,
                    context.Scope);
                if (condition == null)
                {
                    continue;
                }

                context.Writer.Line("if (" + condition + ")");
                context.Writer.Open();
                ApplyStyleAssignmentsToLocal(layer.Assignments, context, styleName);
                context.Writer.Close();
                continue;
            }

            ApplyStyleAssignmentsToLocal(layer.Assignments, context, styleName);
        }
    }

    private void ApplyStyleAssignmentsToLocal(List<MarkupAssignment> assignments, EmitContext context, string styleName)
    {
        for (var i = 0; i < assignments.Count; i++)
        {
            MarkupAssignment assignment = assignments[i];
            switch (assignment.Property)
            {
                case MarkupProperty.Color:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        context.Writer.Line(styleName + " = " + styleName + " with { Color = " + code + " };");
                    }

                    break;
                }
                case MarkupProperty.BackgroundColor:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        context.Writer.Line(styleName + " = " + styleName + " with { BackgroundColor = " + code + " };");
                    }

                    break;
                }
                case MarkupProperty.TextColor:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        context.Writer.Line(styleName + " = " + styleName + " with { TextColor = " + code + " };");
                    }

                    break;
                }
                case MarkupProperty.Padding:
                {
                    int[] values = (int[])assignment.Value;
                    context.Writer.Line(
                        styleName + " = " + styleName + " with { Padding = new global::Reef.UI.Padding(left: " + values[0]
                        + ", top: " + values[1] + ", right: " + values[2] + ", bottom: " + values[3] + ") };");
                    break;
                }
                case MarkupProperty.CornerRadius:
                {
                    int[] values = (int[])assignment.Value;
                    context.Writer.Line(
                        styleName + " = " + styleName + " with { CornerRadius = new global::Reef.UI.CornerRadius(topLeft: " + values[0]
                        + ", topRight: " + values[1] + ", bottomLeft: " + values[2] + ", bottomRight: " + values[3] + ") };");
                    break;
                }
                case MarkupProperty.BorderThickness:
                {
                    int? value = EmitInt(assignment, context);
                    if (value != null)
                    {
                        context.Writer.Line(styleName + " = " + styleName + " with { Border = " + styleName + ".Border with { Thickness = " + value.Value + " } };");
                    }

                    break;
                }
                case MarkupProperty.BorderColor:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        context.Writer.Line(styleName + " = " + styleName + " with { Border = " + styleName + ".Border with { Color = " + code + " } };");
                    }

                    break;
                }
                case MarkupProperty.BorderTopThickness:
                case MarkupProperty.BorderRightThickness:
                case MarkupProperty.BorderBottomThickness:
                case MarkupProperty.BorderLeftThickness:
                {
                    int? value = EmitInt(assignment, context);
                    if (value != null)
                    {
                        string field = WidgetSideField(assignment.Property, "Thickness");
                        context.Writer.Line(styleName + " = " + styleName + " with { Border = " + styleName + ".Border with { " + field + " = " + value.Value + " } };");
                    }

                    break;
                }
                case MarkupProperty.BorderTopColor:
                case MarkupProperty.BorderRightColor:
                case MarkupProperty.BorderBottomColor:
                case MarkupProperty.BorderLeftColor:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        string field = WidgetSideField(assignment.Property, "Color");
                        context.Writer.Line(styleName + " = " + styleName + " with { Border = " + styleName + ".Border with { " + field + " = " + code + " } };");
                    }

                    break;
                }
                case MarkupProperty.CornerCapsLength:
                {
                    int? value = EmitInt(assignment, context);
                    if (value != null)
                    {
                        context.Writer.Line(styleName + " = " + styleName + " with { CornerCaps = " + styleName + ".CornerCaps with { Length = " + value.Value + " } };");
                    }

                    break;
                }
                case MarkupProperty.CornerCapsThickness:
                {
                    int? value = EmitInt(assignment, context);
                    if (value != null)
                    {
                        context.Writer.Line(styleName + " = " + styleName + " with { CornerCaps = " + styleName + ".CornerCaps with { Thickness = " + value.Value + " } };");
                    }

                    break;
                }
                case MarkupProperty.CornerCapsColor:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        context.Writer.Line(styleName + " = " + styleName + " with { CornerCaps = " + styleName + ".CornerCaps with { Color = " + code + " } };");
                    }

                    break;
                }
            }
        }
    }

    private static string WidgetSideField(MarkupProperty property, string suffix)
    {
        return property switch
        {
            MarkupProperty.BorderTopThickness or MarkupProperty.BorderTopColor => "Top" + suffix,
            MarkupProperty.BorderRightThickness or MarkupProperty.BorderRightColor => "Right" + suffix,
            MarkupProperty.BorderBottomThickness or MarkupProperty.BorderBottomColor => "Bottom" + suffix,
            _ => "Left" + suffix,
        };
    }

    private void ApplyAssignmentsToConstraints(StylePlan plan, EmitContext context, string constraintsName)
    {
        ApplyConstraintAssignments(plan.Normal, RssPseudoState.None, context, constraintsName);
        ApplyConstraintAssignments(plan.Inline, RssPseudoState.None, context, constraintsName);
        ApplyConstraintAssignments(plan.Final, RssPseudoState.None, context, constraintsName);
    }

    private void ApplyConstraintAssignments(
        List<StyleLayer> layers,
        RssPseudoState state,
        EmitContext context,
        string constraintsName)
    {
        for (var i = 0; i < layers.Count; i++)
        {
            StyleLayer layer = layers[i];
            if (layer.State != state)
            {
                continue;
            }

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var j = 0; j < layer.Assignments.Count; j++)
            {
                MarkupAssignment assignment = layer.Assignments[j];
                switch (assignment.Property)
                {
                    case MarkupProperty.Width:
                    case MarkupProperty.Height:
                    case MarkupProperty.X:
                    case MarkupProperty.Y:
                    {
                        string property = assignment.Property switch
                        {
                            MarkupProperty.Width => "Width",
                            MarkupProperty.Height => "Height",
                            MarkupProperty.X => "X",
                            _ => "Y",
                        };
                        string? code = EmitConstraint(assignment, context);
                        if (code != null)
                        {
                            fields[property] = property + " = " + code;
                        }

                        break;
                    }
                    case MarkupProperty.MinWidth:
                    case MarkupProperty.MinHeight:
                    case MarkupProperty.MaxWidth:
                    case MarkupProperty.MaxHeight:
                    {
                        int? value = EmitInt(assignment, context);
                        if (value != null)
                        {
                            fields[assignment.Property.ToString()] = assignment.Property + " = " + value.Value;
                        }

                        break;
                    }
                    case MarkupProperty.Anchors:
                    {
                        MarkupAnchors anchors = (MarkupAnchors)assignment.Value;
                        fields["Anchors"] = "Anchors = " + BuildAnchors(anchors);
                        break;
                    }
                }
            }

            if (fields.Count > 0)
            {
                context.Writer.Line(constraintsName + " = " + constraintsName + " with { " + string.Join(", ", fields.Values) + " };");
            }
        }
    }

    private void ApplyAssignmentsToFont(StylePlan plan, EmitContext context, string fontName)
    {
        ApplyFontAssignments(plan.Normal, RssPseudoState.None, context, fontName);
        ApplyFontAssignments(plan.Inline, RssPseudoState.None, context, fontName);
        ApplyFontAssignments(plan.Final, RssPseudoState.None, context, fontName);
    }

    private void ApplyFontAssignments(
        List<StyleLayer> layers,
        RssPseudoState state,
        EmitContext context,
        string fontName)
    {
        for (var i = 0; i < layers.Count; i++)
        {
            StyleLayer layer = layers[i];
            if (layer.State != state)
            {
                continue;
            }

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var j = 0; j < layer.Assignments.Count; j++)
            {
                MarkupAssignment assignment = layer.Assignments[j];
                switch (assignment.Property)
                {
                    case MarkupProperty.FontSize:
                    {
                        int? value = EmitInt(assignment, context);
                        if (value != null)
                        {
                            fields["Size"] = "Size = " + value.Value;
                        }

                        break;
                    }
                    case MarkupProperty.FontID:
                    {
                        string? code = EmitStringValue(assignment, context);
                        if (code != null)
                        {
                            fields["ID"] = "ID = " + code;
                        }

                        break;
                    }
                    case MarkupProperty.HorizontalAlignment:
                    {
                        MarkupHorizontalAlignment alignment = (MarkupHorizontalAlignment)assignment.Value;
                        string value = alignment switch
                        {
                            MarkupHorizontalAlignment.Center => "Center",
                            MarkupHorizontalAlignment.Right => "Right",
                            _ => "Left",
                        };
                        fields["HorizontalAlignment"] = "HorizontalAlignment = global::Reef.UI.HorizontalAlignment." + value;
                        break;
                    }
                    case MarkupProperty.VerticalAlignment:
                    {
                        MarkupVerticalAlignment alignment = (MarkupVerticalAlignment)assignment.Value;
                        string value = alignment switch
                        {
                            MarkupVerticalAlignment.Center => "Center",
                            MarkupVerticalAlignment.Bottom => "Bottom",
                            _ => "Top",
                        };
                        fields["VerticalAlignment"] = "VerticalAlignment = global::Reef.UI.VerticalAlignment." + value;
                        break;
                    }
                    case MarkupProperty.NoWrap:
                    {
                        bool? value = EmitBool(assignment, context);
                        if (value != null)
                        {
                            fields["NoWrap"] = "NoWrap = " + (value.Value ? "true" : "false");
                        }

                        break;
                    }
                }
            }

            if (fields.Count > 0)
            {
                context.Writer.Line(fontName + " = " + fontName + " with { " + string.Join(", ", fields.Values) + " };");
            }
        }
    }

    private bool HasStyleAssignments(StylePlan plan)
    {
        return HasAny(plan, MarkupProperty.Color)
               || HasAny(plan, MarkupProperty.BackgroundColor)
               || HasAny(plan, MarkupProperty.TextColor)
               || HasAny(plan, MarkupProperty.BorderTopThickness)
               || HasAny(plan, MarkupProperty.BorderTopColor)
               || HasAny(plan, MarkupProperty.BorderRightThickness)
               || HasAny(plan, MarkupProperty.BorderRightColor)
               || HasAny(plan, MarkupProperty.BorderBottomThickness)
               || HasAny(plan, MarkupProperty.BorderBottomColor)
               || HasAny(plan, MarkupProperty.BorderLeftThickness)
               || HasAny(plan, MarkupProperty.BorderLeftColor)
               || HasAny(plan, MarkupProperty.Padding)
               || HasAny(plan, MarkupProperty.CornerRadius)
               || HasAny(plan, MarkupProperty.BorderThickness)
               || HasAny(plan, MarkupProperty.BorderColor)
               || HasAny(plan, MarkupProperty.CornerCapsLength)
               || HasAny(plan, MarkupProperty.CornerCapsThickness)
               || HasAny(plan, MarkupProperty.CornerCapsColor);
    }

    private bool HasConstraintAssignments(StylePlan plan)
    {
        return HasAny(plan, MarkupProperty.Width)
               || HasAny(plan, MarkupProperty.Height)
               || HasAny(plan, MarkupProperty.X)
               || HasAny(plan, MarkupProperty.Y)
               || HasAny(plan, MarkupProperty.MinWidth)
               || HasAny(plan, MarkupProperty.MinHeight)
               || HasAny(plan, MarkupProperty.MaxWidth)
               || HasAny(plan, MarkupProperty.MaxHeight)
               || HasAny(plan, MarkupProperty.Anchors);
    }

    private bool HasFontAssignments(StylePlan plan)
    {
        return HasAny(plan, MarkupProperty.FontSize)
               || HasAny(plan, MarkupProperty.FontID)
               || HasAny(plan, MarkupProperty.HorizontalAlignment)
               || HasAny(plan, MarkupProperty.VerticalAlignment)
               || HasAny(plan, MarkupProperty.NoWrap);
    }

    private bool HasLayoutAssignments(StylePlan plan)
    {
        return HasAny(plan, MarkupProperty.Layout) || HasAny(plan, MarkupProperty.Spacing);
    }

    private static bool HasAny(StylePlan plan, MarkupProperty property)
    {
        return HasAny(plan.Normal, property) || HasAny(plan.Inline, property) || HasAny(plan.Final, property);
    }

    private static bool HasAny(List<StyleLayer> layers, MarkupProperty property)
    {
        for (var i = 0; i < layers.Count; i++)
        {
            for (var j = 0; j < layers[i].Assignments.Count; j++)
            {
                if (layers[i].Assignments[j].Property == property)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasParameter(IMethodSymbol method, string name)
    {
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (string.Equals(method.Parameters[i].Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasParameterType(IMethodSymbol method, string typeName)
    {
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (method.Parameters[i].Type.ToDisplayString() == typeName)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAssignable(ITypeSymbol from, ITypeSymbol to)
    {
        if (TypeNames.IsSameType(from, to))
        {
            return true;
        }

        return Compilation.ClassifyConversion(from, to).IsImplicit;
    }

    private static RmlAttribute? FindParameterAttribute(RmlElementNode node, string parameterName)
    {
        string normalized = WidgetResolver.NormalizeParameterName(parameterName);
        for (var i = 0; i < node.Attributes.Count; i++)
        {
            RmlAttribute attribute = node.Attributes[i];
            if (attribute.NamespaceUri.Length != 0)
            {
                continue;
            }

            if (WidgetResolver.NormalizeParameterName(attribute.Name) == normalized)
            {
                return attribute;
            }
        }

        return null;
    }
}
