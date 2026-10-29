using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Reef.Generator;

internal sealed partial class DocumentState
{
    private bool HasReferencedComponent(string tag)
    {
        return FindReferencedComponents(tag, Location.None, report: false).Count > 0;
    }

    private bool TryGetReferencedComponent(
        string tag,
        Location location,
        out INamedTypeSymbol? component,
        out AttributeData? attribute)
    {
        List<ReferencedComponent> components = FindReferencedComponents(tag, location, report: true);
        if (components.Count == 0)
        {
            component = null;
            attribute = null;
            return false;
        }

        if (components.Count > 1)
        {
            Report(
                DiagnosticDescriptors.UnknownElement,
                location,
                tag + " (ambiguous across referenced assemblies)");
            component = null;
            attribute = null;
            return false;
        }

        component = components[0].Type;
        attribute = components[0].Attribute;
        return true;
    }

    private List<ReferencedComponent> FindReferencedComponents(string tag, Location location, bool report)
    {
        var results = new List<ReferencedComponent>();
        var candidates = new List<INamedTypeSymbol>();
        CollectNamedTypes(Compilation.GlobalNamespace, tag, candidates);

        foreach (INamedTypeSymbol type in candidates)
        {
            if (!TypeResolver.IsAccessible(Compilation, type))
            {
                if (report)
                {
                    Report(
                        DiagnosticDescriptors.SlotMismatch,
                        location,
                        "Referenced component '" + tag + "' is not accessible.");
                }

                continue;
            }

            foreach (AttributeData candidate in type.GetAttributes())
            {
                if (candidate.AttributeClass?.ToDisplayString() != "Reef.ReefComponentAttribute")
                {
                    continue;
                }

                if (candidate.ConstructorArguments.Length == 0
                    || candidate.ConstructorArguments[0].Value is not string name
                    || !string.Equals(name, tag, StringComparison.Ordinal))
                {
                    continue;
                }

                results.Add(new ReferencedComponent(type, candidate));
                break;
            }
        }

        return results;
    }

    private static void CollectNamedTypes(INamespaceOrTypeSymbol container, string name, List<INamedTypeSymbol> results)
    {
        foreach (ISymbol member in container.GetMembers())
        {
            if (member is INamedTypeSymbol type)
            {
                if (string.Equals(type.Name, name, StringComparison.Ordinal))
                {
                    results.Add(type);
                }

                CollectNamedTypes(type, name, results);
            }
            else if (member is INamespaceSymbol childNamespace)
            {
                CollectNamedTypes(childNamespace, name, results);
            }
        }
    }

    private void EmitReferencedComponent(
        RmlElementNode call,
        INamedTypeSymbol component,
        AttributeData attribute,
        EmitContext context)
    {
        IMethodSymbol? render = FindComponentRender(component);
        if (render == null)
        {
            Report(
                DiagnosticDescriptors.SlotMismatch,
                call.Location,
                "Referenced component '" + call.Tag + "' has no compatible Render method.");
            return;
        }

        ITypeSymbol? contextType = GetComponentContextType(component, attribute, render);
        RmlAttribute? contextAttribute = HasAttribute(call, "context");
        string contextExpression = string.Empty;
        if (contextType != null)
        {
            if (contextAttribute == null)
            {
                Report(
                    DiagnosticDescriptors.SlotMismatch,
                    call.Location,
                    "Component '" + call.Tag + "' requires a context attribute.");
                return;
            }

            string? code = EmitTypedExpression(contextAttribute.Value, contextType, contextAttribute.Location, context.Scope);
            if (code == null)
            {
                return;
            }

            contextExpression = code;
        }
        else if (contextAttribute != null)
        {
            Report(
                DiagnosticDescriptors.SlotMismatch,
                contextAttribute.Location,
                "Component '" + call.Tag + "' does not declare a Context.");
            return;
        }

        string instanceExpression;
        RmlAttribute? instanceAttribute = HasAttribute(call, "instance");
        if (instanceAttribute != null)
        {
            string? code = EmitTypedExpression(instanceAttribute.Value, component, instanceAttribute.Location, context.Scope);
            if (code == null)
            {
                return;
            }

            instanceExpression = code;
        }
        else
        {
            IMethodSymbol? constructor = FindParameterlessConstructor(component);
            if (constructor == null)
            {
                Report(
                    DiagnosticDescriptors.SlotMismatch,
                    call.Location,
                    "Component '" + call.Tag + "' has no accessible parameterless constructor. Provide instance=\"@expression\".");
                return;
            }

            instanceExpression = "new " + TypeNames.Display(component) + "()";
        }

        CallerPlan callerPlan = BuildCallerPlan(call, context);
        ReportUnsupportedCallerStates(callerPlan, call.Location);

        var styleEntries = new List<StyleEntry>();
        var constraintEntries = new List<ConstraintEntry>();
        CollectOverrideEntries(callerPlan, styleEntries, constraintEntries);

        string? callerStyleName = null;
        if (styleEntries.Count > 0)
        {
            if (!HasRenderParameterType(render, "StyleOverride"))
            {
                Report(
                    DiagnosticDescriptors.SlotMismatch,
                    call.Location,
                    "Component '" + call.Tag + "' does not accept caller styles.");
            }
            else
            {
                callerStyleName = NextLocal("__callerStyle");
                context.Writer.Line("var " + callerStyleName + " = new global::Reef.UI.StyleOverride();");
                EmitStyleOverrideAssignments(styleEntries, context, callerStyleName);
            }
        }

        string? callerConstraintsName = null;
        if (constraintEntries.Count > 0)
        {
            if (!HasRenderParameterType(render, "ConstraintsOverride"))
            {
                Report(
                    DiagnosticDescriptors.SlotMismatch,
                    call.Location,
                    "Component '" + call.Tag + "' does not accept caller constraints.");
            }
            else
            {
                callerConstraintsName = NextLocal("__callerConstraints");
                context.Writer.Line("var " + callerConstraintsName + " = new global::Reef.UI.ConstraintsOverride();");
                EmitConstraintsOverrideAssignments(constraintEntries, context, callerConstraintsName);
            }
        }

        string callSitePrefix = BuildCallSitePrefix(call, context);
        string combinedPrefix = CombineIdPrefix(context.IdPrefix, callSitePrefix);

        Dictionary<string, SlotContent> slotContent = BuildReferencedSlotContent(
            call,
            render,
            context,
            combinedPrefix);

        string? visibleCode = null;
        RmlAttribute? visibleAttribute = HasAttribute(call, "visible");
        if (visibleAttribute != null)
        {
            string value = visibleAttribute.Value.Trim();
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                visibleCode = EmitTypedExpression(
                    value,
                    Compilation.GetSpecialType(SpecialType.System_Boolean),
                    visibleAttribute.Location,
                    context.Scope);
            }
        }

        if (visibleCode != null)
        {
            context.Writer.Line("if (" + visibleCode + ")");
            context.Writer.Open();
        }

        var arguments = new List<string> { context.Builder };
        for (var i = 1; i < render.Parameters.Length; i++)
        {
            IParameterSymbol parameter = render.Parameters[i];
            string typeName = TypeNames.UnwrapNullable(parameter.Type).ToDisplayString();
            if (typeName == "Reef.UI.StyleOverride")
            {
                arguments.Add(parameter.Name + ": " + (callerStyleName ?? "null"));
                continue;
            }

            if (typeName == "Reef.UI.ConstraintsOverride")
            {
                arguments.Add(parameter.Name + ": " + (callerConstraintsName ?? "null"));
                continue;
            }

            if (TypeNames.UnwrapNullable(parameter.Type).SpecialType == SpecialType.System_String
                && string.Equals(parameter.Name, "idPrefix", StringComparison.Ordinal))
            {
                arguments.Add(parameter.Name + ": " + combinedPrefix);
                continue;
            }

            if (IsSlotDelegate(parameter.Type)
                && parameter.Name.StartsWith("content_", StringComparison.Ordinal))
            {
                string slotName = parameter.Name.Substring("content_".Length);
                if (slotContent.TryGetValue(slotName, out SlotContent? slot))
                {
                    arguments.Add(parameter.Name + ": " + BuildSlotLambda(slot));
                }

                continue;
            }

            if (string.Equals(parameter.Name, "context", StringComparison.Ordinal))
            {
                arguments.Add(parameter.Name + ": " + contextExpression);
                continue;
            }

            if (parameter.IsOptional || parameter.HasExplicitDefaultValue)
            {
                continue;
            }

            Report(
                DiagnosticDescriptors.SlotMismatch,
                call.Location,
                "Component Render parameter '" + parameter.Name + "' cannot be supplied from markup.");
        }

        context.Writer.Line(
            instanceExpression + ".Render(" + string.Join(", ", arguments) + ");");

        if (visibleCode != null)
        {
            context.Writer.Close();
        }
    }

    private IMethodSymbol? FindComponentRender(INamedTypeSymbol component)
    {
        IMethodSymbol? fallback = null;
        foreach (ISymbol member in component.GetMembers("Render"))
        {
            if (member is not IMethodSymbol method
                || method.MethodKind != MethodKind.Ordinary
                || method.Parameters.Length == 0)
            {
                continue;
            }

            if (!TypeResolver.IsAccessible(Compilation, method))
            {
                continue;
            }

            if (!WidgetFacts.IsApplicable(RendererType, method))
            {
                continue;
            }

            for (var i = 0; i < method.Parameters.Length; i++)
            {
                if (TypeNames.UnwrapNullable(method.Parameters[i].Type).ToDisplayString() == "Reef.UI.StyleOverride")
                {
                    return method;
                }
            }

            fallback ??= method;
        }

        return fallback;
    }

    private ITypeSymbol? GetComponentContextType(
        INamedTypeSymbol component,
        AttributeData attribute,
        IMethodSymbol render)
    {
        if (attribute.ConstructorArguments.Length >= 2
            && attribute.ConstructorArguments[1].Value is ITypeSymbol contextType)
        {
            return contextType;
        }

        for (var i = 1; i < render.Parameters.Length; i++)
        {
            if (string.Equals(render.Parameters[i].Name, "context", StringComparison.Ordinal))
            {
                return render.Parameters[i].Type;
            }
        }

        return null;
    }

    private IMethodSymbol? FindParameterlessConstructor(INamedTypeSymbol component)
    {
        if (component.IsAbstract || component.TypeKind != TypeKind.Class)
        {
            return null;
        }

        foreach (IMethodSymbol constructor in component.InstanceConstructors)
        {
            if (constructor.Parameters.Length == 0
                && constructor.DeclaredAccessibility != Accessibility.Private
                && constructor.DeclaredAccessibility != Accessibility.Protected
                && TypeResolver.IsAccessible(Compilation, constructor))
            {
                return constructor;
            }
        }

        return null;
    }

    private static bool IsSlotDelegate(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named
            || named.TypeArguments.Length != 1
            || !string.Equals(named.Name, "Action", StringComparison.Ordinal)
            || !string.Equals(named.ContainingNamespace.ToDisplayString(), "System", StringComparison.Ordinal))
        {
            return false;
        }

        return WidgetResolver.IsUIBuilder(named.TypeArguments[0]);
    }

    private static bool HasRenderParameterType(IMethodSymbol render, string simpleTypeName)
    {
        for (var i = 0; i < render.Parameters.Length; i++)
        {
            string display = TypeNames.UnwrapNullable(render.Parameters[i].Type).ToDisplayString();
            if (display.EndsWith("." + simpleTypeName, StringComparison.Ordinal)
                || string.Equals(display, simpleTypeName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private Dictionary<string, SlotContent> BuildReferencedSlotContent(
        RmlElementNode call,
        IMethodSymbol render,
        EmitContext context,
        string idPrefix)
    {
        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < render.Parameters.Length; i++)
        {
            IParameterSymbol parameter = render.Parameters[i];
            if (parameter.Name.StartsWith("content_", StringComparison.Ordinal)
                && IsSlotDelegate(parameter.Type))
            {
                declared[parameter.Name.Substring("content_".Length)] = parameter.Name;
            }
        }

        var content = new Dictionary<string, SlotContent>(StringComparer.Ordinal);
        for (var i = 0; i < call.Children.Count; i++)
        {
            RmlNode child = call.Children[i];
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

            if (!declared.ContainsKey(name))
            {
                Report(
                    DiagnosticDescriptors.UnusedContentSlot,
                    child.Location,
                    name,
                    call.Tag);
                continue;
            }

            if (!content.TryGetValue(name, out SlotContent? slot))
            {
                slot = new SlotContent
                {
                    Scope = context.Scope,
                    Builder = "slotUi",
                    IdPrefix = idPrefix,
                };
                content[name] = slot;
            }

            slot.Nodes.AddRange(nodes);
        }

        if (call.Children.Count > 0 && declared.Count == 0)
        {
            Report(
                DiagnosticDescriptors.SlotMismatch,
                call.Location,
                "Component '" + call.Tag + "' does not declare content slots.");
        }

        return content;
    }

    private string BuildSlotLambda(SlotContent slot)
    {
        var writer = new CodeWriter();
        var slotContext = new EmitContext
        {
            State = this,
            Writer = writer,
            Scope = slot.Scope,
            Builder = slot.Builder,
            IdPrefix = slot.IdPrefix,
            IsRoot = false,
            CallerRuntime = false,
            CallerPlan = null,
            Slots = null,
            LoopDepth = 0,
        };
        EmitNodes(slot.Nodes, slotContext);
        string body = writer.ToString().Replace("\n", " ").Trim();
        return "slotUi => { " + body + " }";
    }

    private void ReportUnsupportedCallerStates(CallerPlan plan, Location location)
    {
        for (var i = 0; i < plan.Final.Count; i++)
        {
            StyleLayer layer = plan.Final[i];
            if (layer.State != RssPseudoState.None)
            {
                Report(
                    DiagnosticDescriptors.UnsupportedConstruct,
                    layer.Location,
                    "Caller pseudo-state classes on a cross-assembly component are not supported.");
            }
        }
    }

    private void CollectOverrideEntries(
        CallerPlan plan,
        List<StyleEntry> styles,
        List<ConstraintEntry> constraints)
    {
        for (var i = 0; i < plan.Final.Count; i++)
        {
            StyleLayer layer = plan.Final[i];
            if (layer.State != RssPseudoState.None)
            {
                continue;
            }

            for (var j = 0; j < layer.Assignments.Count; j++)
            {
                MarkupAssignment assignment = layer.Assignments[j];
                if (IsStyleProperty(assignment.Property))
                {
                    styles.Add(new StyleEntry(assignment, layer.ConditionRaw, layer.Location));
                }
                else if (IsConstraintProperty(assignment.Property))
                {
                    constraints.Add(new ConstraintEntry(assignment, layer.ConditionRaw, layer.Location));
                }
            }
        }
    }

    private void EmitStyleOverrideAssignments(List<StyleEntry> entries, EmitContext context, string name)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            StyleEntry entry = entries[i];
            string? condition = entry.ConditionRaw != null
                ? EmitTypedExpression(
                    entry.ConditionRaw,
                    Compilation.GetSpecialType(SpecialType.System_Boolean),
                    entry.Location,
                    context.Scope)
                : null;
            if (entry.ConditionRaw != null && condition == null)
            {
                continue;
            }

            if (condition != null)
            {
                context.Writer.Line("if (" + condition + ")");
                context.Writer.Open();
            }

            MarkupAssignment assignment = entry.Assignment;
            switch (assignment.Property)
            {
                case MarkupProperty.Color:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        context.Writer.Line(name + ".Color = " + code + ";");
                    }

                    break;
                }
                case MarkupProperty.BackgroundColor:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        context.Writer.Line(name + ".BackgroundColor = " + code + ";");
                    }

                    break;
                }
                case MarkupProperty.TextColor:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        context.Writer.Line(name + ".TextColor = " + code + ";");
                    }

                    break;
                }
                case MarkupProperty.Padding:
                {
                    int[] values = (int[])assignment.Value;
                    context.Writer.Line(name + ".Padding = new global::Reef.UI.Padding(left: " + values[0]
                        + ", top: " + values[1] + ", right: " + values[2] + ", bottom: " + values[3] + ");");
                    break;
                }
                case MarkupProperty.CornerRadius:
                {
                    int[] values = (int[])assignment.Value;
                    context.Writer.Line(name + ".CornerRadius = new global::Reef.UI.CornerRadius(topLeft: " + values[0]
                        + ", topRight: " + values[1] + ", bottomLeft: " + values[2] + ", bottomRight: " + values[3] + ");");
                    break;
                }
                case MarkupProperty.BorderThickness:
                {
                    int? value = EmitInt(assignment, context);
                    if (value != null)
                    {
                        context.Writer.Line(name + ".Border = (" + name + ".Border ?? new global::Reef.UI.BorderStyle()) with { Thickness = " + value.Value + " };");
                    }

                    break;
                }
                case MarkupProperty.BorderColor:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        context.Writer.Line(name + ".Border = (" + name + ".Border ?? new global::Reef.UI.BorderStyle()) with { Color = " + code + " };");
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
                        string field = OverrideSideField(assignment.Property, "Thickness");
                        context.Writer.Line(name + ".Border = (" + name + ".Border ?? new global::Reef.UI.BorderStyle()) with { " + field + " = " + value.Value + " };");
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
                        string field = OverrideSideField(assignment.Property, "Color");
                        context.Writer.Line(name + ".Border = (" + name + ".Border ?? new global::Reef.UI.BorderStyle()) with { " + field + " = " + code + " };");
                    }

                    break;
                }
                case MarkupProperty.CornerCapsLength:
                {
                    int? value = EmitInt(assignment, context);
                    if (value != null)
                    {
                        context.Writer.Line(name + ".CornerCaps = (" + name + ".CornerCaps ?? new global::Reef.UI.CornerCapStyle()) with { Length = " + value.Value + " };");
                    }

                    break;
                }
                case MarkupProperty.CornerCapsThickness:
                {
                    int? value = EmitInt(assignment, context);
                    if (value != null)
                    {
                        context.Writer.Line(name + ".CornerCaps = (" + name + ".CornerCaps ?? new global::Reef.UI.CornerCapStyle()) with { Thickness = " + value.Value + " };");
                    }

                    break;
                }
                case MarkupProperty.CornerCapsColor:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        context.Writer.Line(name + ".CornerCaps = (" + name + ".CornerCaps ?? new global::Reef.UI.CornerCapStyle()) with { Color = " + code + " };");
                    }

                    break;
                }
            }

            if (condition != null)
            {
                context.Writer.Close();
            }
        }
    }

    private void EmitConstraintsOverrideAssignments(List<ConstraintEntry> entries, EmitContext context, string name)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            ConstraintEntry entry = entries[i];
            string? condition = entry.ConditionRaw != null
                ? EmitTypedExpression(
                    entry.ConditionRaw,
                    Compilation.GetSpecialType(SpecialType.System_Boolean),
                    entry.Location,
                    context.Scope)
                : null;
            if (entry.ConditionRaw != null && condition == null)
            {
                continue;
            }

            if (condition != null)
            {
                context.Writer.Line("if (" + condition + ")");
                context.Writer.Open();
            }

            MarkupAssignment assignment = entry.Assignment;
            switch (assignment.Property)
            {
                case MarkupProperty.Width:
                case MarkupProperty.Height:
                case MarkupProperty.X:
                case MarkupProperty.Y:
                {
                    string? code = EmitConstraint(assignment, context);
                    if (code != null)
                    {
                        string property = assignment.Property switch
                        {
                            MarkupProperty.Width => "Width",
                            MarkupProperty.Height => "Height",
                            MarkupProperty.X => "X",
                            _ => "Y",
                        };
                        context.Writer.Line(name + "." + property + " = " + code + ";");
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
                        context.Writer.Line(name + "." + assignment.Property + " = " + value.Value + ";");
                    }

                    break;
                }
                case MarkupProperty.Anchors:
                {
                    MarkupAnchors anchors = (MarkupAnchors)assignment.Value;
                    context.Writer.Line(name + ".Anchors = " + BuildAnchors(anchors) + ";");
                    break;
                }
            }

            if (condition != null)
            {
                context.Writer.Close();
            }
        }
    }

    private static bool IsStyleProperty(MarkupProperty property)
    {
        return property == MarkupProperty.Color
               || property == MarkupProperty.BackgroundColor
               || property == MarkupProperty.TextColor
               || property == MarkupProperty.BorderTopThickness
               || property == MarkupProperty.BorderTopColor
               || property == MarkupProperty.BorderRightThickness
               || property == MarkupProperty.BorderRightColor
               || property == MarkupProperty.BorderBottomThickness
               || property == MarkupProperty.BorderBottomColor
               || property == MarkupProperty.BorderLeftThickness
               || property == MarkupProperty.BorderLeftColor
               || property == MarkupProperty.Padding
               || property == MarkupProperty.CornerRadius
               || property == MarkupProperty.BorderThickness
               || property == MarkupProperty.BorderColor
               || property == MarkupProperty.CornerCapsLength
               || property == MarkupProperty.CornerCapsThickness
               || property == MarkupProperty.CornerCapsColor;
    }

    private static string OverrideSideField(MarkupProperty property, string suffix)
    {
        return property switch
        {
            MarkupProperty.BorderTopThickness or MarkupProperty.BorderTopColor => "Top" + suffix,
            MarkupProperty.BorderRightThickness or MarkupProperty.BorderRightColor => "Right" + suffix,
            MarkupProperty.BorderBottomThickness or MarkupProperty.BorderBottomColor => "Bottom" + suffix,
            _ => "Left" + suffix,
        };
    }

    private static bool IsConstraintProperty(MarkupProperty property)
    {
        return property == MarkupProperty.Width
               || property == MarkupProperty.Height
               || property == MarkupProperty.X
               || property == MarkupProperty.Y
               || property == MarkupProperty.MinWidth
               || property == MarkupProperty.MinHeight
               || property == MarkupProperty.MaxWidth
               || property == MarkupProperty.MaxHeight
               || property == MarkupProperty.Anchors;
    }

    private sealed class ReferencedComponent
    {
        public readonly INamedTypeSymbol Type;
        public readonly AttributeData Attribute;

        public ReferencedComponent(INamedTypeSymbol type, AttributeData attribute)
        {
            Type = type;
            Attribute = attribute;
        }
    }

    private sealed class StyleEntry
    {
        public readonly MarkupAssignment Assignment;
        public readonly string? ConditionRaw;
        public readonly Location Location;

        public StyleEntry(MarkupAssignment assignment, string? conditionRaw, Location location)
        {
            Assignment = assignment;
            ConditionRaw = conditionRaw;
            Location = location;
        }
    }

    private sealed class ConstraintEntry
    {
        public readonly MarkupAssignment Assignment;
        public readonly string? ConditionRaw;
        public readonly Location Location;

        public ConstraintEntry(MarkupAssignment assignment, string? conditionRaw, Location location)
        {
            Assignment = assignment;
            ConditionRaw = conditionRaw;
            Location = location;
        }
    }
}
