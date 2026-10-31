using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Reef.Generator;

internal sealed partial class DocumentState
{
    private void ApplyRuntimeCallerOverrides(RmlElementNode node, EmitContext context)
    {
        if (!context.IsRoot || !context.CallerRuntime || context.RuntimeCallerApplied)
        {
            return;
        }

        context.RuntimeCallerApplied = true;
        string styleName = NextLocal("__callerStyle");
        context.Writer.Line("if (callerStyle is { } " + styleName + ")");
        context.Writer.Open();

        string value = NextLocal("__value");
        context.Writer.Line("if (" + styleName + ".Color is { } " + value + ")");
        context.Writer.Open();
        context.Writer.Line(context.Builder + ".Color = " + value + ";");
        context.Writer.Close();

        value = NextLocal("__value");
        context.Writer.Line("if (" + styleName + ".TextColor is { } " + value + ")");
        context.Writer.Open();
        context.Writer.Line(context.Builder + ".TextColor = " + value + ";");
        context.Writer.Close();

        value = NextLocal("__value");
        context.Writer.Line("if (" + styleName + ".BackgroundColor is { } " + value + ")");
        context.Writer.Open();
        if (!string.Equals(node.Tag, "Text", StringComparison.Ordinal)
            && !string.Equals(node.Tag, "Image", StringComparison.Ordinal))
        {
            context.Writer.Line(context.Builder + ".Color = " + value + ";");
        }

        context.Writer.Line(context.Builder + ".BackgroundColor = " + value + ";");
        context.Writer.Close();

        value = NextLocal("__value");
        context.Writer.Line("if (" + styleName + ".Padding is { } " + value + ")");
        context.Writer.Open();
        context.Writer.Line(context.Builder + ".Padding = " + value + ";");
        context.Writer.Close();

        value = NextLocal("__value");
        context.Writer.Line("if (" + styleName + ".CornerRadius is { } " + value + ")");
        context.Writer.Open();
        context.Writer.Line(context.Builder + ".CornerRadius = " + value + ";");
        context.Writer.Close();

        value = NextLocal("__value");
        context.Writer.Line("if (" + styleName + ".Border is { } " + value + ")");
        context.Writer.Open();
        context.Writer.Line(context.Builder + ".Border = " + value + ";");
        context.Writer.Close();

        value = NextLocal("__value");
        context.Writer.Line("if (" + styleName + ".CornerCaps is { } " + value + ")");
        context.Writer.Open();
        context.Writer.Line(context.Builder + ".CornerCaps = " + value + ";");
        context.Writer.Close();

        context.Writer.Close();

        string constraintsName = NextLocal("__callerConstraints");
        context.Writer.Line("if (callerConstraints is { } " + constraintsName + ")");
        context.Writer.Open();
        WriteConstraintOverride(context.Writer, context.Builder, constraintsName);
        context.Writer.Close();
    }

    private static void WriteConstraintOverride(CodeWriter writer, string builder, string constraintsName)
    {
        string local = "__c";
        writer.Line("global::Reef.UI.Constraints " + local + " = " + builder + ".Constraints;");
        writer.Line("if (" + constraintsName + ".Anchors is { } anchors) " + local + ".Anchors = anchors;");
        writer.Line("if (" + constraintsName + ".X != null) " + local + ".X = " + constraintsName + ".X;");
        writer.Line("if (" + constraintsName + ".Y != null) " + local + ".Y = " + constraintsName + ".Y;");
        writer.Line("if (" + constraintsName + ".Width != null) " + local + ".Width = " + constraintsName + ".Width;");
        writer.Line("if (" + constraintsName + ".Height != null) " + local + ".Height = " + constraintsName + ".Height;");
        writer.Line("if (" + constraintsName + ".MinWidth is { } minWidth) " + local + ".MinWidth = minWidth;");
        writer.Line("if (" + constraintsName + ".MinHeight is { } minHeight) " + local + ".MinHeight = minHeight;");
        writer.Line("if (" + constraintsName + ".MaxWidth is { } maxWidth) " + local + ".MaxWidth = maxWidth;");
        writer.Line("if (" + constraintsName + ".MaxHeight is { } maxHeight) " + local + ".MaxHeight = maxHeight;");
        writer.Line(builder + ".Constraints = " + local + ";");
    }

    private void EmitClip(RmlElementNode node, EmitContext context)
    {
        RmlAttribute? clip = HasAttribute(node, "clip");
        if (clip != null)
        {
            string value = clip.Value.Trim();
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                context.Writer.Line(
                    context.Builder + ".ClipConstraints = new global::Reef.UI.Constraints { Width = new global::Reef.UI.Relative(1f), Height = new global::Reef.UI.Relative(1f) };");
            }
            else if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                context.Writer.Line(context.Builder + ".ClipConstraints = null;");
            }
            else
            {
                List<RssToken> tokens = StyleValueParser.TokenizeValue(value);
                List<RssToken> numbers = NonTrivia(tokens);
                if (numbers.Count == 2
                    && int.TryParse(numbers[0].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width)
                    && int.TryParse(numbers[1].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int height))
                {
                    context.Writer.Line(
                        context.Builder + ".ClipConstraints = new global::Reef.UI.Constraints { Width = new global::Reef.Constraints.Fixed("
                        + width + "), Height = new global::Reef.Constraints.Fixed(" + height + ") };");
                }
                else
                {
                    Report(DiagnosticDescriptors.InvalidShorthand, clip.Location, value, "Clip requires 'true', 'false', or two integers.");
                }
            }
        }

        EmitClipDimension(node, context, "clip-width", "Width");
        EmitClipDimension(node, context, "clip-height", "Height");
    }

    private void EmitClipDimension(RmlElementNode node, EmitContext context, string attributeName, string property)
    {
        RmlAttribute? attribute = HasAttribute(node, attributeName);
        if (attribute == null)
        {
            return;
        }

        string? code = EmitTypedExpression(
            attribute.Value,
            Compilation.GetSpecialType(SpecialType.System_Int32),
            attribute.Location,
            context.Scope);
        if (code == null)
        {
            return;
        }

        context.Writer.Line(
            context.Builder + ".ClipConstraints = (" + context.Builder + ".ClipConstraints ?? new global::Reef.UI.Constraints()) with { "
            + property + " = new global::Reef.Constraints.Fixed(" + code + ") };");
    }

    private void EmitScroll(RmlElementNode node, EmitContext context)
    {
        RmlAttribute? scroll = HasAttribute(node, "scroll");
        if (scroll != null)
        {
            string value = scroll.Value.Trim().ToLowerInvariant();
            switch (value)
            {
                case "none":
                    context.Writer.Line(context.Builder + ".VerticalScroll = false;");
                    context.Writer.Line(context.Builder + ".HorizontalScroll = false;");
                    break;
                case "vertical":
                    context.Writer.Line(context.Builder + ".VerticalScroll = true;");
                    break;
                case "horizontal":
                    context.Writer.Line(context.Builder + ".HorizontalScroll = true;");
                    break;
                case "both":
                    context.Writer.Line(context.Builder + ".VerticalScroll = true;");
                    context.Writer.Line(context.Builder + ".HorizontalScroll = true;");
                    break;
                default:
                    Report(DiagnosticDescriptors.InvalidEnumValue, scroll.Location, scroll.Value, "scroll");
                    break;
            }
        }

        EmitScrollOffset(node, context, "scroll-x", "ScrollX");
        EmitScrollOffset(node, context, "scroll-y", "ScrollY");

        if (HasAttribute(node, "onscroll") != null && scroll == null)
        {
            Report(DiagnosticDescriptors.ScrollConfiguration, node.Location, "onscroll");
        }
    }

    private void EmitScrollOffset(RmlElementNode node, EmitContext context, string attributeName, string property)
    {
        RmlAttribute? attribute = HasAttribute(node, attributeName);
        if (attribute == null)
        {
            return;
        }

        string? code = EmitTypedExpression(
            attribute.Value,
            Compilation.GetSpecialType(SpecialType.System_Int32),
            attribute.Location,
            context.Scope);
        if (code != null)
        {
            context.Writer.Line(context.Builder + "." + property + " = " + code + ";");
        }
    }

    private void EmitFocus(RmlElementNode node, EmitContext context)
    {
        RmlAttribute? attribute = HasAttribute(node, "focus");
        if (attribute == null)
        {
            return;
        }

        bool? value = EmitBoolAttribute(attribute, context);
        if (value == true)
        {
            context.Writer.Line(context.Builder + ".Focus();");
        }
        else if (value == null)
        {
            string? code = EmitTypedExpression(
                attribute.Value,
                Compilation.GetSpecialType(SpecialType.System_Boolean),
                attribute.Location,
                context.Scope);
            if (code != null)
            {
                context.Writer.Line("if (" + code + ")");
                context.Writer.Open();
                context.Writer.Line(context.Builder + ".Focus();");
                context.Writer.Close();
            }
        }
    }

    private void EmitPassthrough(RmlElementNode node, EmitContext context)
    {
        RmlAttribute? attribute = HasAttribute(node, "passthrough");
        if (attribute == null)
        {
            return;
        }

        bool? value = EmitBoolAttribute(attribute, context);
        if (value != null)
        {
            context.Writer.Line(context.Builder + ".Passthrough = " + (value.Value ? "true" : "false") + ";");
        }
    }

    private bool? EmitBoolAttribute(RmlAttribute attribute, EmitContext context)
    {
        string value = attribute.Value.Trim();
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? code = EmitTypedExpression(
            value,
            Compilation.GetSpecialType(SpecialType.System_Boolean),
            attribute.Location,
            context.Scope);
        if (code == "true")
        {
            return true;
        }

        if (code == "false")
        {
            return false;
        }

        return null;
    }

    private void EmitCallbacks(RmlElementNode node, EmitContext context, string? disabled)
    {
        EmitCallbacksFromAttributes(node.Attributes, node, context, disabled);
    }

    private void EmitCallbacksFromAttributes(
        List<RmlAttribute> attributes,
        RmlElementNode node,
        EmitContext context,
        string? disabled)
    {
        RmlAttribute? buttonAttribute = FindAttribute(attributes, "button");
        string button = "global::Reef.UIController.MouseButtons.Left";
        if (buttonAttribute != null)
        {
            switch (buttonAttribute.Value.Trim().ToLowerInvariant())
            {
                case "left":
                    button = "global::Reef.UIController.MouseButtons.Left";
                    break;
                case "right":
                    button = "global::Reef.UIController.MouseButtons.Right";
                    break;
                case "middle":
                    button = "global::Reef.UIController.MouseButtons.Middle";
                    break;
                default:
                    Report(DiagnosticDescriptors.InvalidEnumValue, buttonAttribute.Location, buttonAttribute.Value, "button");
                    break;
            }
        }

        RmlAttribute? hotkeyAttribute = FindAttribute(attributes, "hotkey") ?? FindAttribute(attributes, "key");
        string? hotkeyCode = null;
        if (hotkeyAttribute != null)
        {
            var binder = new ExpressionBinder(context.Scope);
            if (binder.TryBindHotkey(hotkeyAttribute.Value, hotkeyAttribute.Location, out BoundExpression key))
            {
                hotkeyCode = key.Code;
            }
        }

        EmitClickCallback(attributes, node, context, disabled, "onclick", button, hotkeyCode);
        EmitClickCallback(attributes, node, context, disabled, "onrightclick", "global::Reef.UIController.MouseButtons.Right", null);
        EmitClickCallback(attributes, node, context, disabled, "onmiddleclick", "global::Reef.UIController.MouseButtons.Middle", null);

        RmlAttribute? heldAttribute = FindAttribute(attributes, "onheld");
        if (heldAttribute != null)
        {
            var binder = new ExpressionBinder(context.Scope);
            if (binder.TryBindCallback(heldAttribute.Value, heldAttribute.Location, out BoundCallback callback))
            {
                EmitGuarded(context, disabled, context.Builder + ".Held(" + button + ")", callback.Code);
            }
        }

        RmlAttribute? scrollAttribute = FindAttribute(attributes, "onscroll");
        if (scrollAttribute != null)
        {
            var binder = new ExpressionBinder(context.Scope);
            if (binder.TryBindCallback(scrollAttribute.Value, scrollAttribute.Location, out BoundCallback callback))
            {
                EmitGuarded(context, disabled, context.Builder + ".Scrolled()", callback.Code);
            }
        }

        RmlAttribute? enterAttribute = FindAttribute(attributes, "onenter");
        if (enterAttribute != null)
        {
            var binder = new ExpressionBinder(context.Scope);
            if (binder.TryBindCallback(enterAttribute.Value, enterAttribute.Location, out BoundCallback callback))
            {
                EmitGuarded(context, disabled, context.Builder + ".Entered()", callback.Code);
            }
        }

        RmlAttribute? exitAttribute = FindAttribute(attributes, "onexit");
        if (exitAttribute != null)
        {
            var binder = new ExpressionBinder(context.Scope);
            if (binder.TryBindCallback(exitAttribute.Value, exitAttribute.Location, out BoundCallback callback))
            {
                EmitGuarded(context, disabled, context.Builder + ".Exited()", callback.Code);
            }
        }
    }

    private void EmitClickCallback(
        List<RmlAttribute> attributes,
        RmlElementNode node,
        EmitContext context,
        string? disabled,
        string attributeName,
        string button,
        string? hotkeyCode)
    {
        RmlAttribute? attribute = FindAttribute(attributes, attributeName);
        if (attribute == null)
        {
            return;
        }

        var binder = new ExpressionBinder(context.Scope);
        if (!binder.TryBindCallback(attribute.Value, attribute.Location, out BoundCallback callback))
        {
            return;
        }

        string condition = context.Builder + ".Clicked(" + button + ")";
        if (hotkeyCode != null && string.Equals(attributeName, "onclick", StringComparison.Ordinal))
        {
            string hotkeyName = NextLocal("__hotkey");
            context.Writer.Line("bool " + hotkeyName + " = " + context.Builder + ".IsPressed(" + hotkeyCode + ");");
            condition = "(" + condition + " || " + hotkeyName + ")";
        }

        EmitGuarded(context, disabled, condition, callback.Code);
    }

    private static void EmitGuarded(EmitContext context, string? disabled, string condition, string code)
    {
        string guard = disabled != null ? "!" + disabled + " && (" + condition + ")" : condition;
        context.Writer.Line("if (" + guard + ")");
        context.Writer.Open();
        context.Writer.Line(code + ";");
        context.Writer.Close();
    }

    private static RmlAttribute? FindAttribute(List<RmlAttribute> attributes, string name)
    {
        for (var i = 0; i < attributes.Count; i++)
        {
            RmlAttribute attribute = attributes[i];
            if (attribute.NamespaceUri.Length == 0
                && string.Equals(attribute.Name, name, StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        return null;
    }

    private bool IsKeyBoundLoopId(RmlElementNode node, EmitContext context)
    {
        RmlAttribute? id = HasAttribute(node, "id");
        if (id == null)
        {
            return false;
        }

        string idValue = id.Value.TrimStart();
        if (context.InComponent
            && idValue.StartsWith("@(", StringComparison.Ordinal)
            && idValue.IndexOf("param.", System.StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        if (id.Value.IndexOf('{') < 0)
        {
            return false;
        }

        for (var i = 0; i < context.LoopItems.Count; i++)
        {
            if (id.Value.IndexOf(context.LoopItems[i], StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        for (var i = 0; i < context.LoopIndexes.Count; i++)
        {
            if (id.Value.IndexOf(context.LoopIndexes[i], StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void ReportInlineStyleError(string property, string value, Location location, string error)
    {
        switch (property.ToLowerInvariant())
        {
            case "color":
            case "background":
            case "border-color":
            case "corner-caps-color":
                Report(DiagnosticDescriptors.InvalidColor, location, value);
                break;
            case "width":
            case "height":
            case "min-width":
            case "min-height":
            case "max-width":
            case "max-height":
            case "size":
            case "min-size":
            case "max-size":
            case "x":
            case "y":
            case "pos":
            case "position":
                Report(DiagnosticDescriptors.InvalidConstraint, location, value, error);
                break;
            case "font-size":
                Report(DiagnosticDescriptors.InvalidDimension, location, value, error);
                break;
            case "padding":
            case "corner-radius":
            case "border":
            case "border-width":
            case "corner-caps":
            case "corner-caps-length":
            case "corner-caps-thickness":
                Report(DiagnosticDescriptors.InvalidShorthand, location, value, error);
                break;
            case "layout":
            case "text-align":
            case "vertical-align":
            case "anchor":
            case "anchors":
                Report(DiagnosticDescriptors.InvalidEnumValue, location, value, property);
                break;
            default:
                Report(DiagnosticDescriptors.UnknownStyleProperty, location, property);
                break;
        }
    }

    private string? EmitStringValue(MarkupAssignment assignment, EmitContext context)
    {
        if (assignment.Value is string text)
        {
            return FnvHash.EscapeString(text);
        }

        if (assignment.Value is MarkupExpression expression)
        {
            return EmitTypedExpression(
                expression.Raw,
                Compilation.GetSpecialType(SpecialType.System_String),
                assignment.Location,
                context.Scope);
        }

        return null;
    }

    private static List<RssToken> NonTrivia(IReadOnlyList<RssToken> tokens)
    {
        var result = new List<RssToken>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind != RssTokenKind.Whitespace)
            {
                result.Add(tokens[i]);
            }
        }

        return result;
    }

    private void ValidateAttributes(RmlElementNode node, HashSet<string> tagAttributes, string tag)
    {
        for (var i = 0; i < node.Attributes.Count; i++)
        {
            RmlAttribute attribute = node.Attributes[i];
            if (attribute.NamespaceUri == "http://www.w3.org/XML/1998/namespace"
                || attribute.NamespaceUri == "http://www.w3.org/2001/XMLSchema-instance"
                || attribute.IsClassDirective
                || attribute.IsParameterAttribute)
            {
                continue;
            }

            if (attribute.NamespaceUri.Length > 0)
            {
                Report(
                    DiagnosticDescriptors.UnknownAttribute,
                    attribute.Location,
                    attribute.QualifiedName,
                    tag,
                    "Namespaced attributes are only supported for class directives.");
                continue;
            }

            if (!MarkupVocabulary.COMMON_ATTRIBUTES.Contains(attribute.Name)
                && !tagAttributes.Contains(attribute.Name))
            {
                Report(
                    DiagnosticDescriptors.UnknownAttribute,
                    attribute.Location,
                    attribute.Name,
                    tag,
                    "The attribute is not valid on this tag.");
            }
        }
    }

    private void EmitLocalComponent(RmlElementNode call, RmlDocument component, EmitContext context)
    {
        if (!_inlineStack.Add(component.ClassName))
        {
            Report(
                DiagnosticDescriptors.CircularComponent,
                call.Location,
                component.ClassName,
                component.ClassName);
            return;
        }

        try
        {
            RmlAttribute? instance = HasAttribute(call, "instance");
            if (instance != null)
            {
                Report(
                    DiagnosticDescriptors.UnsupportedConstruct,
                    instance.Location,
                    "Local components are inlined and cannot receive an instance.");
                return;
            }

            ValidateComponentCallAttributes(call);

            ITypeSymbol? childContextType = GetChildContextType(component);
            RmlAttribute? contextAttribute = HasAttribute(call, "context");
            string childContextExpression = string.Empty;
            if (childContextType != null)
            {
                if (contextAttribute == null)
                {
                    Report(
                        DiagnosticDescriptors.SlotMismatch,
                        call.Location,
                        "Component '" + component.ClassName + "' requires a context attribute.");
                    return;
                }

                string? code = EmitTypedExpression(contextAttribute.Value, childContextType, contextAttribute.Location, context.Scope);
                if (code == null)
                {
                    return;
                }

                childContextExpression = code;
            }
            else if (contextAttribute != null)
            {
                Report(
                    DiagnosticDescriptors.SlotMismatch,
                    contextAttribute.Location,
                    "Component '" + component.ClassName + "' does not declare a Context.");
                return;
            }

            ITypeSymbol? childParameterType = GetChildParameterType(component);
            string? parameterObject = null;
            var parameterAttributes = new List<RmlAttribute>();
            for (var i = 0; i < call.Attributes.Count; i++)
            {
                if (call.Attributes[i].IsParameterAttribute)
                {
                    parameterAttributes.Add(call.Attributes[i]);
                }
            }

            if (childParameterType == null && parameterAttributes.Count > 0)
            {
                Report(
                    DiagnosticDescriptors.UnknownAttribute,
                    parameterAttributes[0].Location,
                    parameterAttributes[0].QualifiedName,
                    call.Tag,
                    "The component does not declare parameters.");
                return;
            }

            if (childParameterType != null)
            {
                parameterObject = BuildParameterObject(call, childParameterType, parameterAttributes, context);
                if (parameterObject == null)
                {
                    return;
                }
            }

            if (component.RendererDataType != null)
            {
                string componentNamespace = TypeResolver.InferNamespace(component, Compilation, _options);
                ITypeSymbol? componentRenderer = TypeResolver.ResolveType(Compilation, component.RendererDataType, componentNamespace);
                if (componentRenderer != null
                    && RendererType != null
                    && !TypeNames.IsSameType(componentRenderer, RendererType))
                {
                    Report(
                        DiagnosticDescriptors.SlotMismatch,
                        call.Location,
                        "Component renderer data type does not match the caller.");
                    return;
                }
            }

            Dictionary<string, SlotInfo> slotDefinitions = new(StringComparer.Ordinal);
            CollectSlots(component.Roots, slotDefinitions, new List<SlotInfo>());

            Dictionary<string, SlotContent>? slotContent = BuildSlotContent(call, component, slotDefinitions, context);
            if (slotContent == null)
            {
                return;
            }

            CallerPlan callerPlan = BuildCallerPlan(call, context);
            string callSitePrefix = BuildCallSitePrefix(call, context);
            string combinedPrefix = CombineIdPrefix(context.IdPrefix, callSitePrefix);

            RmlAttribute? visibleAttribute = HasAttribute(call, "visible");
            string? visibleCode = null;
            bool visibleLiteral = false;
            if (visibleAttribute != null)
            {
                string value = visibleAttribute.Value.Trim();
                if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    visibleLiteral = true;
                }
                else if (!string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                {
                    visibleCode = EmitTypedExpression(
                        value,
                        Compilation.GetSpecialType(SpecialType.System_Boolean),
                        visibleAttribute.Location,
                        context.Scope);
                }
            }

            if (visibleLiteral)
            {
                return;
            }

            if (visibleCode != null)
            {
                context.Writer.Line("if (" + visibleCode + ")");
                context.Writer.Open();
            }

            ExpressionScope childScope = context.Scope.CloneWith(
                childContextExpression,
                context.Builder,
                childContextType);
            if (childContextExpression.Length > 0)
            {
                string root = ExpressionScope.GetRootIdentifier(childContextExpression);
                if (root.Length > 0
                    && string.Equals(root, childContextExpression.Trim(), StringComparison.Ordinal))
                {
                    childScope.Variables[root] = childContextType ?? Compilation.ObjectType;
                }
            }

            int parameterMark = -1;
            if (parameterObject != null && childParameterType != null)
            {
                context.Writer.Open();
                childScope.Variables["param"] = childParameterType;
                childScope.ParameterName = context.State.NextLocal("__param");
                context.Writer.Line("var " + childScope.ParameterName + " = " + parameterObject + ";");
                parameterMark = context.Writer.Mark();
            }

            EmitContext childContext = context.Clone(childScope);
            childContext.IsRoot = false;
            childContext.CallerRuntime = false;
            childContext.InComponent = true;
            childContext.IdPrefix = combinedPrefix;
            childContext.CallerPlan = callerPlan;
            childContext.Slots = slotContent;
            EmitNodes(component.Roots, childContext);

            if (parameterObject != null && childParameterType != null)
            {
                context.Writer.Close();
                context.Writer.RenameParamFrom(parameterMark, childScope.ParameterName);
            }

            if (visibleCode != null)
            {
                context.Writer.Close();
            }
        }
        finally
        {
            _inlineStack.Remove(component.ClassName);
        }
    }

    private string? BuildParameterObject(
        RmlElementNode call,
        ITypeSymbol parameterType,
        List<RmlAttribute> attributes,
        EmitContext context)
    {
        var assignments = new List<string>();
        for (var i = 0; i < attributes.Count; i++)
        {
            RmlAttribute attribute = attributes[i];
            string name = attribute.Name;
            IPropertySymbol? property = null;
            IFieldSymbol? field = null;
            for (INamedTypeSymbol? current = parameterType as INamedTypeSymbol; current != null; current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers())
                {
                    if (!string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase)
                        || member.IsStatic)
                    {
                        continue;
                    }

                    if (member is IPropertySymbol candidate && candidate.SetMethod != null)
                    {
                        property = candidate;
                        break;
                    }

                    if (member is IFieldSymbol candidateField && !candidateField.IsReadOnly && !candidateField.IsConst)
                    {
                        field = candidateField;
                        break;
                    }
                }

                if (property != null || field != null)
                {
                    break;
                }
            }

            ITypeSymbol? memberType = property?.Type ?? field?.Type;
            string? memberName = property?.Name ?? field?.Name;
            if (memberType == null || memberName == null)
            {
                Report(
                    DiagnosticDescriptors.UnknownAttribute,
                    attribute.Location,
                    attribute.QualifiedName,
                    call.Tag,
                    "The component parameter does not exist.");
                return null;
            }

            string? value = EmitParameterValue(attribute, memberType, context);
            if (value == null)
            {
                return null;
            }

            assignments.Add(memberName + " = " + value);
        }

        string typeName = TypeNames.Display(parameterType);
        return assignments.Count == 0
            ? "new " + typeName + "()"
            : "new " + typeName + " { " + string.Join(", ", assignments) + " }";
    }

    private string? EmitParameterValue(RmlAttribute attribute, ITypeSymbol type, EmitContext context)
    {
        string raw = attribute.Value.Trim();
        if (TypeNames.IsString(type))
        {
            return EmitStringExpression(attribute.Value, attribute.Location, context.Scope);
        }

        if (TypeNames.IsBool(type))
        {
            if (bool.TryParse(raw, out bool literal))
            {
                return literal ? "true" : "false";
            }

            return EmitTypedExpression(attribute.Value, type, attribute.Location, context.Scope);
        }

        if (TypeNames.IsInteger(type)
            || type.SpecialType == SpecialType.System_Single
            || type.SpecialType == SpecialType.System_Double
            || type.SpecialType == SpecialType.System_Decimal)
        {
            return EmitTypedExpression(attribute.Value, type, attribute.Location, context.Scope);
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            if (raw.StartsWith("@", StringComparison.Ordinal))
            {
                return EmitTypedExpression(attribute.Value, type, attribute.Location, context.Scope);
            }

            for (INamedTypeSymbol? current = type as INamedTypeSymbol; current != null; current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers())
                {
                    if (member is IFieldSymbol field
                        && field.HasConstantValue
                        && string.Equals(field.Name, raw, StringComparison.OrdinalIgnoreCase))
                    {
                        return TypeNames.Display(type) + "." + field.Name;
                    }
                }
            }

            Report(
                DiagnosticDescriptors.InvalidEnumValue,
                attribute.Location,
                raw,
                TypeNames.Display(type));
            return null;
        }

        if (TypeNames.Display(type) == "System.Numerics.Vector4")
        {
            if (raw.StartsWith("@", StringComparison.Ordinal))
            {
                return EmitTypedExpression(attribute.Value, type, attribute.Location, context.Scope);
            }

            if (ColorParser.TryParse(StyleValueParser.TokenizeValue(raw), out System.Numerics.Vector4 color, out _))
            {
                return FnvHash.FormatVector(color);
            }

            Report(
                DiagnosticDescriptors.TypeMismatch,
                attribute.Location,
                "Expected a color value.");
            return null;
        }

        if (raw.StartsWith("@", StringComparison.Ordinal))
        {
            return EmitTypedExpression(attribute.Value, type, attribute.Location, context.Scope);
        }

        if (type is INamedTypeSymbol { DelegateInvokeMethod: not null }
            && context.Scope.FindCompanionMethod(raw) != null)
        {
            return raw;
        }

        Report(
            DiagnosticDescriptors.TypeMismatch,
            attribute.Location,
            "Parameter '" + attribute.QualifiedName + "' requires an expression.");
        return null;
    }

    private void ValidateComponentCallAttributes(RmlElementNode call)
    {
        for (var i = 0; i < call.Attributes.Count; i++)
        {
            RmlAttribute attribute = call.Attributes[i];
            if (attribute.NamespaceUri == "http://www.w3.org/XML/1998/namespace"
                || attribute.NamespaceUri == "http://www.w3.org/2001/XMLSchema-instance"
                || attribute.IsClassDirective
                || attribute.IsParameterAttribute)
            {
                continue;
            }

            if (attribute.NamespaceUri.Length > 0)
            {
                Report(DiagnosticDescriptors.UnknownAttribute, attribute.Location, attribute.QualifiedName, call.Tag, "Namespaced attributes are not supported here.");
                continue;
            }

            if (!MarkupVocabulary.COMPONENT_CALL_ATTRIBUTES.Contains(attribute.Name)
                && !MarkupVocabulary.STYLE_ATTRIBUTES.Contains(attribute.Name)
                && !string.Equals(attribute.Name, "class", StringComparison.Ordinal)
                && !string.Equals(attribute.Name, "visible", StringComparison.Ordinal))
            {
                Report(DiagnosticDescriptors.UnknownAttribute, attribute.Location, attribute.Name, call.Tag, "The attribute is not valid on a component call.");
            }
        }
    }

    private CallerPlan BuildCallerPlan(RmlElementNode call, EmitContext context)
    {
        var plan = new CallerPlan();
        RmlAttribute? classAttribute = HasAttribute(call, "class");
        if (classAttribute != null)
        {
            string[] classes = SplitClasses(classAttribute.Value);
            for (var i = 0; i < classes.Length; i++)
            {
                AddCallerClassLayers(plan, classes[i], classAttribute.Location, null);
            }
        }

        for (var i = 0; i < call.Attributes.Count; i++)
        {
            RmlAttribute attribute = call.Attributes[i];
            if (attribute.IsClassDirective)
            {
                AddCallerClassLayers(plan, attribute.Name, attribute.Location, attribute.Value);
                continue;
            }

            if (attribute.NamespaceUri.Length == 0 && MarkupVocabulary.IsStyleAttribute(attribute.Name))
            {
                AddCallerInlineLayer(plan, call, attribute, context);
            }

            if (attribute.NamespaceUri.Length == 0
                && (string.Equals(attribute.Name, "onclick", StringComparison.Ordinal)
                    || string.Equals(attribute.Name, "onrightclick", StringComparison.Ordinal)
                    || string.Equals(attribute.Name, "onmiddleclick", StringComparison.Ordinal)
                    || string.Equals(attribute.Name, "onheld", StringComparison.Ordinal)
                    || string.Equals(attribute.Name, "onenter", StringComparison.Ordinal)
                    || string.Equals(attribute.Name, "onexit", StringComparison.Ordinal)
                    || string.Equals(attribute.Name, "onscroll", StringComparison.Ordinal)
                    || string.Equals(attribute.Name, "hotkey", StringComparison.Ordinal)
                    || string.Equals(attribute.Name, "key", StringComparison.Ordinal)
                    || string.Equals(attribute.Name, "button", StringComparison.Ordinal)))
            {
                plan.ExtraCallbacks.Add(attribute);
            }
        }

        RmlAttribute? disabled = HasAttribute(call, "disabled");
        if (disabled != null)
        {
            plan.Disabled = EmitTypedExpression(
                disabled.Value,
                Compilation.GetSpecialType(SpecialType.System_Boolean),
                disabled.Location,
                context.Scope);
        }

        RmlAttribute? id = HasAttribute(call, "id");
        if (id != null)
        {
            plan.RootId = EmitStringExpression(id.Value, id.Location, context.Scope);
        }

        return plan;
    }

    private static string? CombineDisabled(string? first, string? second)
    {
        if (first == null)
        {
            return second;
        }

        if (second == null)
        {
            return first;
        }

        return "(" + first + " || " + second + ")";
    }

    private void AddCallerClassLayers(CallerPlan plan, string className, Location location, string? condition)
    {
        if (!_styleCatalog.HasClass(className))
        {
            Report(DiagnosticDescriptors.UnknownStyleClass, location, className);
            return;
        }

        AddCallerClassState(plan, className, RssPseudoState.None, location, condition);
        AddCallerClassState(plan, className, RssPseudoState.Hover, location, condition);
        AddCallerClassState(plan, className, RssPseudoState.Focus, location, condition);
        AddCallerClassState(plan, className, RssPseudoState.Active, location, condition);
        AddCallerClassState(plan, className, RssPseudoState.Disabled, location, condition);
    }

    private void AddCallerClassState(CallerPlan plan, string className, RssPseudoState state, Location location, string? condition)
    {
        if (_styleCatalog.TryGetAssignments(className, state, out List<MarkupAssignment> assignments)
            && assignments.Count > 0)
        {
            var layer = new StyleLayer(state, location, condition);
            layer.Assignments.AddRange(assignments);
            plan.Final.Add(layer);
        }
    }

    private void AddCallerInlineLayer(CallerPlan plan, RmlElementNode call, RmlAttribute attribute, EmitContext context)
    {
        List<RssToken> tokens;
        string? expansionError;
        if (attribute.Value.TrimStart().StartsWith("@", StringComparison.Ordinal))
        {
            tokens = StyleValueParser.TokenizeValue(attribute.Value);
        }
        else
        {
            _styleCatalog.TryExpandInlineValue(attribute.Value, attribute.Location, out tokens, out expansionError);
            if (expansionError.Length > 0)
            {
                Report(DiagnosticDescriptors.StyleSyntax, attribute.Location, expansionError);
                return;
            }
        }

        if (StyleValueParser.TryParseInlineTokens(call, attribute.Name, tokens, attribute.Location, out List<MarkupAssignment> assignments, out string error))
        {
            if (assignments.Count > 0)
            {
                var layer = new StyleLayer(RssPseudoState.None, attribute.Location);
                layer.Assignments.AddRange(assignments);
                plan.Final.Add(layer);
            }
        }
        else if (error != "Attribute is not a style attribute.")
        {
            ReportInlineStyleError(attribute.Name, attribute.Value, attribute.Location, error);
        }
    }

    private Dictionary<string, SlotContent>? BuildSlotContent(
        RmlElementNode call,
        RmlDocument component,
        Dictionary<string, SlotInfo> definitions,
        EmitContext context)
    {
        var content = new Dictionary<string, SlotContent>(StringComparer.Ordinal);
        bool hasAnyChild = false;
        for (var i = 0; i < call.Children.Count; i++)
        {
            RmlNode child = call.Children[i];
            hasAnyChild = true;
            string name = "default";
            List<RmlNode> nodes;
            if (child is RmlContentNode contentNode)
            {
                name = string.IsNullOrWhiteSpace(contentNode.Name) ? "default" : contentNode.Name!.Trim();
                nodes = contentNode.Children;
            }
            else
            {
                nodes = [child];
            }

            if (!definitions.ContainsKey(name))
            {
                Report(
                    DiagnosticDescriptors.UnusedContentSlot,
                    child.Location,
                    name,
                    component.ClassName);
                continue;
            }

            if (!content.TryGetValue(name, out SlotContent? slot))
            {
                slot = new SlotContent
                {
                    Scope = context.Scope,
                    Builder = context.Builder,
                    IdPrefix = context.IdPrefix,
                };
                content[name] = slot;
            }

            slot.Nodes.AddRange(nodes);
        }

        if (hasAnyChild && definitions.Count == 0)
        {
            Report(
                DiagnosticDescriptors.SlotMismatch,
                call.Location,
                "Component '" + component.ClassName + "' does not declare any content slots.");
            return null;
        }

        return content;
    }

    private string BuildCallSitePrefix(RmlElementNode call, EmitContext context)
    {
        RmlAttribute? id = HasAttribute(call, "id");
        if (id != null)
        {
            string? code = EmitStringExpression(id.Value, id.Location, context.Scope);
            if (code != null)
            {
                return code;
            }
        }

        string fileKey = GetFileKey();
        string literal = "\"__rml_" + fileKey + "_" + call.Line + "_" + call.Column + "\"";
        string prefix = literal;
        for (var i = 0; i < context.LoopIndexes.Count; i++)
        {
            prefix = "(" + prefix + " + \"_\" + " + context.LoopIndexes[i] + ")";
        }

        return prefix;
    }


}
