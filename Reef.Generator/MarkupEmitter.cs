using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Reef.Generator;

internal sealed partial class DocumentState
{
    private void EmitNodes(IReadOnlyList<RmlNode> nodes, EmitContext context)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            EmitNode(nodes[i], context);
        }
    }

    private void EmitNode(RmlNode node, EmitContext context)
    {
        switch (node)
        {
            case RmlElementNode element:
                if (element.Prefix.Length > 0)
                {
                    EmitExternalWidget(element, context);
                    return;
                }

                if (string.Equals(element.Tag, "Element", StringComparison.Ordinal))
                {
                    EmitElementNode(element, context);
                    return;
                }

                if (string.Equals(element.Tag, "Text", StringComparison.Ordinal))
                {
                    EmitTextNode(element, context);
                    return;
                }

                if (string.Equals(element.Tag, "Image", StringComparison.Ordinal))
                {
                    EmitImageNode(element, context);
                    return;
                }

                if (TryGetLocalComponent(element.Tag, out RmlDocument? component))
                {
                    EmitLocalComponent(element, component!, context);
                    return;
                }

                if (TryGetReferencedComponent(element.Tag, element.Location, out INamedTypeSymbol? referencedComponent, out AttributeData? componentAttribute))
                {
                    EmitReferencedComponent(element, referencedComponent!, componentAttribute!, context);
                    return;
                }

                if (string.Equals(element.Tag, "If", StringComparison.Ordinal)
                    || string.Equals(element.Tag, "ElseIf", StringComparison.Ordinal)
                    || string.Equals(element.Tag, "Else", StringComparison.Ordinal))
                {
                    Report(
                        DiagnosticDescriptors.ConditionalOrder,
                        element.Location,
                        "'" + element.Tag + "' is not grouped with a valid conditional chain.");
                    return;
                }

                if (string.Equals(element.Tag, "Content", StringComparison.Ordinal))
                {
                    Report(
                        DiagnosticDescriptors.UnsupportedConstruct,
                        element.Location,
                        "<Content> must be parsed as a content node.");
                    return;
                }

                Report(DiagnosticDescriptors.UnknownElement, element.Location, element.Tag);
                return;
            case RmlConditionalNode conditional:
                EmitConditional(conditional, context);
                return;
            case RmlForEachNode forEach:
                EmitForEach(forEach, context);
                return;
            case RmlContentNode content:
                EmitContent(content, context);
                return;
            case RmlEmptyNode empty:
                Report(DiagnosticDescriptors.InvalidEmpty, empty.Location, "<Empty> must be a direct child of <ForEach>.");
                return;
        }
    }

    private void EmitElementNode(RmlElementNode node, EmitContext context)
    {
        ValidateAttributes(node, MarkupVocabulary.ELEMENT_ATTRIBUTES, "Element");
        if (HasAttribute(node, "size") == null)
        {
            //  Element supports the size shorthand.
        }

        StylePlan plan = BuildStylePlan(node, context, out bool needsId);
        string? visibleCode = EmitBoolCondition(node, "visible", context, out bool visibleLiteral);
        if (visibleLiteral && string.Equals(visibleCode, "false", StringComparison.Ordinal))
        {
            return;
        }

        bool applyCaller = context.CallerPlan != null && !context.CallerPlan.Applied;
        string? idCode = null;
        if (applyCaller && context.CallerPlan!.RootId != null)
        {
            idCode = context.CallerPlan.RootId;
        }
        else
        {
            idCode = EmitElementId(node, context, needsId);
        }

        bool requiresKeyBoundId = HasInteractiveAttributes(node)
                                  || HasPseudo(plan, RssPseudoState.Hover)
                                  || HasPseudo(plan, RssPseudoState.Focus)
                                  || HasPseudo(plan, RssPseudoState.Active);
        if (requiresKeyBoundId && context.LoopDepth > 0 && !IsKeyBoundLoopId(node, context))
        {
            Report(
                DiagnosticDescriptors.LoopIdRequired,
                node.Location,
                "Add an id such as id=\"item_{i}\" that references a loop variable.");
        }

        if (visibleCode != null)
        {
            context.Writer.Line("if (" + visibleCode + ")");
            context.Writer.Open();
        }

        context.Writer.Line(idCode == null
            ? "using (" + context.Builder + ".Element())"
            : "using (" + context.Builder + ".Element(" + idCode + "))");
        context.Writer.Open();

        if (applyCaller && context.CallerPlan != null)
        {
            plan.Final.AddRange(context.CallerPlan.Final);
            context.CallerPlan.Applied = true;
        }

        PseudoVariables pseudo = ApplyStylePlan(node, plan, context);
        ApplyRuntimeCallerOverrides(node, context);
        EmitClip(node, context);
        EmitScroll(node, context);
        EmitPassthrough(node, context);
        EmitNodes(node.Children, context);
        string? disabled = CombineDisabled(pseudo.Disabled, context.CallerPlan?.Disabled);
        EmitCallbacks(node, context, disabled);

        if (applyCaller && context.CallerPlan != null)
        {
            EmitCallbacksFromAttributes(context.CallerPlan.ExtraCallbacks, node, context, disabled);
        }

        context.Writer.Close();
        if (visibleCode != null)
        {
            context.Writer.Close();
        }
    }

    private void EmitTextNode(RmlElementNode node, EmitContext context)
    {
        ValidateAttributes(node, MarkupVocabulary.TEXT_ATTRIBUTES, "Text");
        StylePlan plan = BuildStylePlan(node, context, out bool needsId);
        string? visibleCode = EmitBoolCondition(node, "visible", context, out bool visibleLiteral);
        if (visibleLiteral && string.Equals(visibleCode, "false", StringComparison.Ordinal))
        {
            return;
        }

        string? textCode = EmitTextValue(node, context);
        if (textCode == null)
        {
            return;
        }

        bool applyCaller = context.CallerPlan != null && !context.CallerPlan.Applied;
        string? idCode = null;
        if (applyCaller && context.CallerPlan!.RootId != null)
        {
            idCode = context.CallerPlan.RootId;
        }
        else if (needsId)
        {
            idCode = BuildDerivedId(node, context.IdPrefix);
        }

        if (visibleCode != null)
        {
            context.Writer.Line("if (" + visibleCode + ")");
            context.Writer.Open();
        }

        context.Writer.Line("using (" + context.Builder + ".Text(" + textCode + "))");
        context.Writer.Open();
        if (idCode != null)
        {
            context.Writer.Line(context.Builder + ".ID = " + idCode + ";");
        }

        if (applyCaller && context.CallerPlan != null)
        {
            plan.Final.AddRange(context.CallerPlan.Final);
            context.CallerPlan.Applied = true;
        }

        ApplyStylePlan(node, plan, context);
        ApplyRuntimeCallerOverrides(node, context);
        context.Writer.Close();
        if (visibleCode != null)
        {
            context.Writer.Close();
        }
    }

    private void EmitImageNode(RmlElementNode node, EmitContext context)
    {
        ValidateAttributes(node, MarkupVocabulary.IMAGE_ATTRIBUTES, "Image");
        RmlAttribute? source = HasAttribute(node, "source");
        if (source == null)
        {
            Report(DiagnosticDescriptors.MissingAttribute, node.Location, "Image", "source");
            return;
        }

        if (RendererType == null)
        {
            Report(
                DiagnosticDescriptors.UnsupportedConstruct,
                source.Location,
                "<Image> requires the Reef root attribute RendererData.");
            return;
        }

        string? sourceCode = EmitTypedExpression(source.Value, RendererType, source.Location, context.Scope);
        if (sourceCode == null)
        {
            return;
        }

        StylePlan plan = BuildStylePlan(node, context, out bool needsId);
        string? visibleCode = EmitBoolCondition(node, "visible", context, out bool visibleLiteral);
        if (visibleLiteral && string.Equals(visibleCode, "false", StringComparison.Ordinal))
        {
            return;
        }

        bool applyCaller = context.CallerPlan != null && !context.CallerPlan.Applied;
        string? idCode = null;
        if (applyCaller && context.CallerPlan!.RootId != null)
        {
            idCode = context.CallerPlan.RootId;
        }
        else if (needsId)
        {
            idCode = BuildDerivedId(node, context.IdPrefix);
        }

        if (visibleCode != null)
        {
            context.Writer.Line("if (" + visibleCode + ")");
            context.Writer.Open();
        }

        context.Writer.Line("using (" + context.Builder + ".Image(" + sourceCode + "))");
        context.Writer.Open();
        if (idCode != null)
        {
            context.Writer.Line(context.Builder + ".ID = " + idCode + ";");
        }

        if (applyCaller && context.CallerPlan != null)
        {
            plan.Final.AddRange(context.CallerPlan.Final);
            context.CallerPlan.Applied = true;
        }

        ApplyStylePlan(node, plan, context);
        ApplyRuntimeCallerOverrides(node, context);
        EmitNodes(node.Children, context);
        context.Writer.Close();
        if (visibleCode != null)
        {
            context.Writer.Close();
        }
    }

    private void EmitConditional(RmlConditionalNode node, EmitContext context)
    {
        for (var i = 0; i < node.Branches.Count; i++)
        {
            RmlConditionalBranch branch = node.Branches[i];
            if (branch.Condition == null)
            {
                context.Writer.Line(i == 0 ? "if (true)" : "else");
                context.Writer.Open();
                EmitNodes(branch.Children, context);
                context.Writer.Close();
                continue;
            }

            string? condition = EmitTypedExpression(branch.Condition, Compilation.GetSpecialType(SpecialType.System_Boolean), branch.Location, context.Scope);
            if (condition == null)
            {
                continue;
            }

            if (i == 0)
            {
                context.Writer.Line("if (" + condition + ")");
            }
            else
            {
                context.Writer.Line("else if (" + condition + ")");
            }

            context.Writer.Open();
            EmitNodes(branch.Children, context);
            context.Writer.Close();
        }
    }

    private void EmitForEach(RmlForEachNode node, EmitContext context)
    {
        if (string.IsNullOrWhiteSpace(node.InExpression))
        {
            Report(DiagnosticDescriptors.MissingAttribute, node.Location, "ForEach", "in");
            return;
        }

        if (string.IsNullOrWhiteSpace(node.AsName))
        {
            Report(DiagnosticDescriptors.MissingAttribute, node.Location, "ForEach", "as");
            return;
        }

        var binder = new ExpressionBinder(context.Scope);
        if (!binder.TryBind(node.InExpression!, null, node.Location, out BoundExpression collection))
        {
            return;
        }

        if (collection.Type == null)
        {
            Report(DiagnosticDescriptors.TypeMismatch, node.Location, "ForEach collection has no type.");
            return;
        }

        ITypeSymbol? elementType = TypeResolver.GetEnumerableElementType(Compilation, collection.Type);
        if (elementType == null)
        {
            Report(
                DiagnosticDescriptors.TypeMismatch,
                node.Location,
                "ForEach expression must return an enumerable collection.");
            return;
        }

        string itemName = node.AsName!.Trim();
        if (!TypeResolver.IsValidIdentifier(itemName)
            || string.Equals(itemName, "ui", StringComparison.Ordinal)
            || string.Equals(itemName, "context", StringComparison.Ordinal)
            || context.Scope.Variables.ContainsKey(itemName))
        {
            Report(DiagnosticDescriptors.InvalidRoot, node.Location, "ForEach 'as' must be a unique C# identifier.");
            return;
        }

        string? indexName = null;
        if (!string.IsNullOrWhiteSpace(node.IndexName))
        {
            indexName = node.IndexName!.Trim();
            if (!TypeResolver.IsValidIdentifier(indexName) || context.Scope.Variables.ContainsKey(indexName))
            {
                Report(DiagnosticDescriptors.InvalidRoot, node.Location, "ForEach 'index' must be a unique C# identifier.");
                return;
            }
        }

        string internalIndex = indexName ?? NextLocal("__i");
        bool hasEmpty = node.EmptyChildren.Count > 0 || node.EmptyLocation != Location.None;
        string hasAny = NextLocal("__hasAny");
        if (indexName != null)
        {
            context.Writer.Line("int " + indexName + " = 0;");
        }
        else
        {
            context.Writer.Line("int " + internalIndex + " = 0;");
        }

        if (hasEmpty)
        {
            context.Writer.Line("bool " + hasAny + " = false;");
        }

        context.Writer.Line("foreach (var " + itemName + " in " + collection.Code + ")");
        context.Writer.Open();
        if (hasEmpty)
        {
            context.Writer.Line(hasAny + " = true;");
        }

        var variables = new Dictionary<string, ITypeSymbol>(context.Scope.Variables, StringComparer.Ordinal)
        {
            [itemName] = elementType,
        };
        if (indexName != null)
        {
            variables[indexName] = Compilation.GetSpecialType(SpecialType.System_Int32);
        }

        ExpressionScope childScope = context.Scope.CloneWith(
            context.Scope.ContextExpression,
            context.Scope.BuilderExpression,
            context.Scope.ContextType);
        foreach (KeyValuePair<string, ITypeSymbol> pair in variables)
        {
            childScope.Variables[pair.Key] = pair.Value;
        }

        EmitContext childContext = context.Clone(childScope);
        childContext.LoopDepth = context.LoopDepth + 1;
        childContext.LoopIndexes.Add(internalIndex);
        childContext.LoopItems.Add(itemName);
        EmitNodes(node.Children, childContext);
        context.Writer.Line(internalIndex + "++;");
        context.Writer.Close();

        if (hasEmpty)
        {
            context.Writer.Line("if (!" + hasAny + ")");
            context.Writer.Open();
            EmitNodes(node.EmptyChildren, context);
            context.Writer.Close();
        }
    }

    private void EmitContent(RmlContentNode node, EmitContext context)
    {
        string name = string.IsNullOrWhiteSpace(node.Name) ? "default" : node.Name!.Trim();
        if (context.Slots != null && context.Slots.TryGetValue(name, out SlotContent? slotContent))
        {
            EmitContext slotContext = context.Clone(slotContent.Scope);
            slotContext.Builder = slotContent.Builder;
            slotContext.IdPrefix = slotContent.IdPrefix;
            slotContext.CallerPlan = null;
            EmitNodes(slotContent.Nodes, slotContext);
            return;
        }

        string parameter = "content_" + FnvHash.SanitizeIdentifier(name);
        bool declaresSlot = false;
        for (var i = 0; i < _slots.Count; i++)
        {
            if (string.Equals(_slots[i].Name, name, StringComparison.Ordinal))
            {
                declaresSlot = true;
                parameter = _slots[i].ParameterName;
                break;
            }
        }

        if (declaresSlot && !context.IsRoot)
        {
            //  Local inlining uses the slot map. A local component with no
            //  caller content falls back to its default children.
            EmitNodes(node.Children, context);
            return;
        }

        if (declaresSlot)
        {
            context.Writer.Line("if (" + parameter + " != null)");
            context.Writer.Open();
            context.Writer.Line(parameter + "(" + context.Builder + ");");
            context.Writer.Close();
            if (node.Children.Count > 0)
            {
                context.Writer.Line("else");
                context.Writer.Open();
                EmitNodes(node.Children, context);
                context.Writer.Close();
            }

            return;
        }

        EmitNodes(node.Children, context);
    }

    private StylePlan BuildStylePlan(RmlElementNode node, EmitContext context, out bool needsId)
    {
        var plan = new StylePlan();
        needsId = false;
        RmlAttribute? classAttribute = HasAttribute(node, "class");
        if (classAttribute != null)
        {
            string[] classes = SplitClasses(classAttribute.Value);
            for (var i = 0; i < classes.Length; i++)
            {
                AddClassLayers(plan, classes[i], classAttribute.Location, conditionRaw: null);
            }
        }

        for (var i = 0; i < node.Attributes.Count; i++)
        {
            RmlAttribute attribute = node.Attributes[i];
            if (!attribute.IsClassDirective)
            {
                continue;
            }

            if (!IsValidClassDirectiveName(attribute.Name))
            {
                Report(
                    DiagnosticDescriptors.InvalidClassDirective,
                    attribute.Location,
                    attribute.QualifiedName,
                    "Class names may contain letters, digits, underscores, and hyphens only.");
                continue;
            }

            AddClassLayers(plan, attribute.Name, attribute.Location, attribute.Value);
        }

        RmlAttribute? idAttribute = HasAttribute(node, "id");
        if (idAttribute != null)
        {
            needsId = true;
        }

        if (HasAttribute(node, "onclick") != null
            || HasAttribute(node, "onrightclick") != null
            || HasAttribute(node, "onmiddleclick") != null
            || HasAttribute(node, "onheld") != null
            || HasAttribute(node, "onscroll") != null
            || HasAttribute(node, "hotkey") != null
            || HasAttribute(node, "key") != null
            || HasAttribute(node, "scroll") != null)
        {
            needsId = true;
        }

        StyleLayer? inlineLayer = null;
        for (var i = 0; i < node.Attributes.Count; i++)
        {
            RmlAttribute attribute = node.Attributes[i];
            if (attribute.NamespaceUri.Length != 0 || !MarkupVocabulary.IsStyleAttribute(attribute.Name))
            {
                continue;
            }

            List<MarkupAssignment> assignments;
            string error;
            if (attribute.Value.TrimStart().StartsWith("@", StringComparison.Ordinal))
            {
                if (!StyleValueParser.TryParseInline(node, attribute, out assignments, out error))
                {
                    if (error != "Attribute is not a style attribute.")
                    {
                        ReportInlineStyleError(attribute.Name, attribute.Value, attribute.Location, error);
                    }

                    continue;
                }
            }
            else
            {
                _styleCatalog.TryExpandInlineValue(attribute.Value, attribute.Location, out List<RssToken> tokens, out string expansionError);
                if (expansionError.Length > 0)
                {
                    Report(DiagnosticDescriptors.StyleSyntax, attribute.Location, expansionError);
                    continue;
                }

                if (!StyleValueParser.TryParseInlineTokens(node, attribute.Name, tokens, attribute.Location, out assignments, out error))
                {
                    if (error != "Attribute is not a style attribute.")
                    {
                        ReportInlineStyleError(attribute.Name, attribute.Value, attribute.Location, error);
                    }

                    continue;
                }
            }

            if (assignments.Count > 0)
            {
                if (inlineLayer == null)
                {
                    inlineLayer = new StyleLayer(RssPseudoState.None, attribute.Location);
                    plan.Inline.Add(inlineLayer);
                }

                inlineLayer.Assignments.AddRange(assignments);
            }
        }

        if (context.CallerPlan != null && !context.CallerPlan.Applied)
        {
            plan.Final.AddRange(context.CallerPlan.Final);
            if (context.CallerPlan.RootId != null)
            {
                needsId = true;
            }
        }

        for (var i = 0; i < plan.Normal.Count; i++)
        {
            StyleLayer layer = plan.Normal[i];
            if (layer.State == RssPseudoState.Hover
                || layer.State == RssPseudoState.Focus
                || layer.State == RssPseudoState.Active)
            {
                needsId = true;
            }
        }

        for (var i = 0; i < plan.Final.Count; i++)
        {
            StyleLayer layer = plan.Final[i];
            if (layer.State == RssPseudoState.Hover
                || layer.State == RssPseudoState.Focus
                || layer.State == RssPseudoState.Active)
            {
                needsId = true;
            }
        }

        return plan;
    }

    private static bool HasInteractiveAttributes(RmlElementNode node)
    {
        return HasAttribute(node, "onclick") != null
               || HasAttribute(node, "onrightclick") != null
               || HasAttribute(node, "onmiddleclick") != null
               || HasAttribute(node, "onheld") != null
               || HasAttribute(node, "onscroll") != null
               || HasAttribute(node, "hotkey") != null
               || HasAttribute(node, "key") != null
               || HasAttribute(node, "scroll") != null;
    }

    private static bool IsValidClassDirectiveName(string name)
    {
        if (name.Length == 0 || (!char.IsLetter(name[0]) && name[0] != '_'))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            char c = name[i];
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
            {
                return false;
            }
        }

        return true;
    }

    private void AddClassLayers(StylePlan plan, string className, Location location, string? conditionRaw)
    {
        if (className.Length == 0)
        {
            return;
        }

        if (!_styleCatalog.HasClass(className))
        {
            Report(DiagnosticDescriptors.UnknownStyleClass, location, className);
            return;
        }

        if (_styleCatalog.TryGetAssignments(className, RssPseudoState.None, out List<MarkupAssignment> normal)
            && normal.Count > 0)
        {
            var layer = new StyleLayer(RssPseudoState.None, location, conditionRaw);
            layer.Assignments.AddRange(normal);
            plan.Normal.Add(layer);
        }

        AddPseudoLayer(plan, className, RssPseudoState.Hover, location, conditionRaw);
        AddPseudoLayer(plan, className, RssPseudoState.Focus, location, conditionRaw);
        AddPseudoLayer(plan, className, RssPseudoState.Active, location, conditionRaw);
        AddPseudoLayer(plan, className, RssPseudoState.Disabled, location, conditionRaw);
    }

    private void AddPseudoLayer(StylePlan plan, string className, RssPseudoState state, Location location, string? conditionRaw)
    {
        if (_styleCatalog.TryGetAssignments(className, state, out List<MarkupAssignment> assignments)
            && assignments.Count > 0)
        {
            var layer = new StyleLayer(state, location, conditionRaw);
            layer.Assignments.AddRange(assignments);
            plan.Normal.Add(layer);
        }
    }

    private PseudoVariables ApplyStylePlan(RmlElementNode node, StylePlan plan, EmitContext context)
    {
        var pseudo = new PseudoVariables();
        bool hasDisabled = HasPseudo(plan, RssPseudoState.Disabled);
        bool hasHover = HasPseudo(plan, RssPseudoState.Hover);
        bool hasFocus = HasPseudo(plan, RssPseudoState.Focus);
        bool hasActive = HasPseudo(plan, RssPseudoState.Active);

        RmlAttribute? disabled = HasAttribute(node, "disabled");
        if (disabled != null)
        {
            pseudo.Disabled = NextLocal("__disabled");
            string? code = EmitTypedExpression(disabled.Value, Compilation.GetSpecialType(SpecialType.System_Boolean), disabled.Location, context.Scope);
            context.Writer.Line("bool " + pseudo.Disabled + " = " + (code ?? "false") + ";");
        }
        else if (hasDisabled)
        {
            pseudo.Disabled = NextLocal("__disabled");
            context.Writer.Line("bool " + pseudo.Disabled + " = false;");
        }

        if (hasHover)
        {
            pseudo.Hover = NextLocal("__hovering");
            context.Writer.Line(
                "bool " + pseudo.Hover + " = "
                + (pseudo.Disabled != null ? "!" + pseudo.Disabled + " && " : string.Empty)
                + context.Builder + ".Hovering();");
        }

        if (hasFocus)
        {
            pseudo.Focus = NextLocal("__focusing");
            context.Writer.Line(
                "bool " + pseudo.Focus + " = "
                + (pseudo.Disabled != null ? "!" + pseudo.Disabled + " && " : string.Empty)
                + context.Builder + ".Focused();");
        }

        if (hasActive)
        {
            pseudo.Active = NextLocal("__active");
            context.Writer.Line(
                "bool " + pseudo.Active + " = "
                + (pseudo.Disabled != null ? "!" + pseudo.Disabled + " && " : string.Empty)
                + context.Builder + ".Held();");
        }

        ApplyLayers(plan.Normal, RssPseudoState.None, node, context);
        ApplyState(plan.Normal, RssPseudoState.Hover, pseudo.Hover, node, context);
        ApplyState(plan.Normal, RssPseudoState.Focus, pseudo.Focus, node, context);
        ApplyState(plan.Normal, RssPseudoState.Active, pseudo.Active, node, context);
        ApplyState(plan.Normal, RssPseudoState.Disabled, pseudo.Disabled, node, context);
        ApplyLayers(plan.Inline, RssPseudoState.None, node, context);
        ApplyLayers(plan.Final, RssPseudoState.None, node, context);
        ApplyState(plan.Final, RssPseudoState.Hover, pseudo.Hover, node, context);
        ApplyState(plan.Final, RssPseudoState.Focus, pseudo.Focus, node, context);
        ApplyState(plan.Final, RssPseudoState.Active, pseudo.Active, node, context);
        ApplyState(plan.Final, RssPseudoState.Disabled, pseudo.Disabled, node, context);
        return pseudo;
    }

    private void ApplyState(List<StyleLayer> layers, RssPseudoState state, string? stateVariable, RmlElementNode node, EmitContext context)
    {
        if (stateVariable == null || !HasAnyState(layers, state))
        {
            return;
        }

        context.Writer.Line("if (" + stateVariable + ")");
        context.Writer.Open();
        ApplyLayers(layers, state, node, context);
        context.Writer.Close();
    }

    private static bool HasAnyState(List<StyleLayer> layers, RssPseudoState state)
    {
        for (var i = 0; i < layers.Count; i++)
        {
            if (layers[i].State == state && layers[i].Assignments.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasPseudo(StylePlan plan, RssPseudoState state)
    {
        for (var i = 0; i < plan.Normal.Count; i++)
        {
            if (plan.Normal[i].State == state && plan.Normal[i].Assignments.Count > 0)
            {
                return true;
            }
        }

        for (var i = 0; i < plan.Final.Count; i++)
        {
            if (plan.Final[i].State == state && plan.Final[i].Assignments.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyLayers(List<StyleLayer> layers, RssPseudoState state, RmlElementNode node, EmitContext context)
    {
        var unconditionalAssignments = new List<MarkupAssignment>();

        for (var i = 0; i < layers.Count; i++)
        {
            StyleLayer layer = layers[i];
            if (layer.State != state || layer.Assignments.Count == 0)
            {
                continue;
            }

            if (layer.ConditionRaw != null)
            {
                if (unconditionalAssignments.Count > 0)
                {
                    ApplyAssignments(unconditionalAssignments, node, context);
                    unconditionalAssignments.Clear();
                }

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
                ApplyAssignments(layer.Assignments, node, context);
                context.Writer.Close();
                continue;
            }

            unconditionalAssignments.AddRange(layer.Assignments);
        }

        if (unconditionalAssignments.Count > 0)
        {
            ApplyAssignments(unconditionalAssignments, node, context);
        }
    }

    private void ApplyAssignments(List<MarkupAssignment> assignments, RmlElementNode node, EmitContext context)
    {
        string builder = context.Builder;
        var directAssignments = new List<MarkupAssignment>();
        var borderFields = new Dictionary<string, string>(StringComparer.Ordinal);
        var cornerCapsFields = new Dictionary<string, string>(StringComparer.Ordinal);
        var fontFields = new Dictionary<string, string>(StringComparer.Ordinal);
        var constraintFields = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < assignments.Count; i++)
        {
            MarkupAssignment assignment = assignments[i];
            switch (assignment.Property)
            {
                case MarkupProperty.BorderThickness:
                {
                    int? value = EmitInt(assignment, context);
                    if (value != null)
                    {
                        borderFields["Thickness"] = "Thickness = " + value.Value;
                    }
                    break;
                }
                case MarkupProperty.BorderColor:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        borderFields["Color"] = "Color = " + code;
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
                        borderFields[SideField(assignment.Property, "Thickness")] = SideField(assignment.Property, "Thickness") + " = " + value.Value;
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
                        borderFields[SideField(assignment.Property, "Color")] = SideField(assignment.Property, "Color") + " = " + code;
                    }
                    break;
                }
                case MarkupProperty.CornerCapsLength:
                {
                    int? value = EmitInt(assignment, context);
                    if (value != null)
                    {
                        cornerCapsFields["Length"] = "Length = " + value.Value;
                    }
                    break;
                }
                case MarkupProperty.CornerCapsThickness:
                {
                    int? value = EmitInt(assignment, context);
                    if (value != null)
                    {
                        cornerCapsFields["Thickness"] = "Thickness = " + value.Value;
                    }
                    break;
                }
                case MarkupProperty.CornerCapsColor:
                {
                    string? code = EmitColor(assignment, context);
                    if (code != null)
                    {
                        cornerCapsFields["Color"] = "Color = " + code;
                    }
                    break;
                }
                case MarkupProperty.FontSize:
                {
                    int? value = EmitInt(assignment, context);
                    if (value != null)
                    {
                        fontFields["Size"] = "Size = " + value.Value;
                    }
                    break;
                }
                case MarkupProperty.FontID:
                {
                    string? code = EmitStringValue(assignment, context);
                    if (code != null)
                    {
                        fontFields["ID"] = "ID = " + code;
                    }
                    break;
                }
                case MarkupProperty.HorizontalAlignment:
                {
                    MarkupHorizontalAlignment value = (MarkupHorizontalAlignment)assignment.Value;
                    string enumValue = value switch
                    {
                        MarkupHorizontalAlignment.Center => "Center",
                        MarkupHorizontalAlignment.Right => "Right",
                        _ => "Left",
                    };
                    fontFields["HorizontalAlignment"] = "HorizontalAlignment = global::Reef.UI.HorizontalAlignment." + enumValue;
                    break;
                }
                case MarkupProperty.VerticalAlignment:
                {
                    MarkupVerticalAlignment value = (MarkupVerticalAlignment)assignment.Value;
                    string enumValue = value switch
                    {
                        MarkupVerticalAlignment.Center => "Center",
                        MarkupVerticalAlignment.Bottom => "Bottom",
                        _ => "Top",
                    };
                    fontFields["VerticalAlignment"] = "VerticalAlignment = global::Reef.UI.VerticalAlignment." + enumValue;
                    break;
                }
                case MarkupProperty.NoWrap:
                {
                    bool? value = EmitBool(assignment, context);
                    if (value != null)
                    {
                        fontFields["NoWrap"] = "NoWrap = " + (value.Value ? "true" : "false");
                    }
                    break;
                }
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
                        constraintFields[property] = property + " = " + code;
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
                        string property = assignment.Property.ToString();
                        constraintFields[property] = property + " = " + value.Value;
                    }
                    break;
                }
                case MarkupProperty.Anchors:
                {
                    MarkupAnchors anchors = (MarkupAnchors)assignment.Value;
                    constraintFields["Anchors"] = "Anchors = " + BuildAnchors(anchors);
                    break;
                }
                default:
                    directAssignments.Add(assignment);
                    break;
            }
        }

        for (var i = 0; i < directAssignments.Count; i++)
        {
            ApplyDirectAssignment(directAssignments[i], node, context);
        }

        if (borderFields.Count > 0)
        {
            context.Writer.Line(builder + ".Border = " + builder + ".Border with { " + string.Join(", ", borderFields.Values) + " };");
        }

        if (cornerCapsFields.Count > 0)
        {
            context.Writer.Line(builder + ".CornerCaps = " + builder + ".CornerCaps with { " + string.Join(", ", cornerCapsFields.Values) + " };");
        }

        if (fontFields.Count > 0)
        {
            context.Writer.Line(builder + ".FontOptions = " + builder + ".FontOptions with { " + string.Join(", ", fontFields.Values) + " };");
        }

        if (constraintFields.Count > 0)
        {
            context.Writer.Line(builder + ".Constraints = " + builder + ".Constraints with { " + string.Join(", ", constraintFields.Values) + " };");
        }
    }

    private static string SideField(MarkupProperty property, string suffix)
    {
        return property switch
        {
            MarkupProperty.BorderTopThickness or MarkupProperty.BorderTopColor => "Top" + suffix,
            MarkupProperty.BorderRightThickness or MarkupProperty.BorderRightColor => "Right" + suffix,
            MarkupProperty.BorderBottomThickness or MarkupProperty.BorderBottomColor => "Bottom" + suffix,
            _ => "Left" + suffix,
        };
    }

    private void ApplyDirectAssignment(MarkupAssignment assignment, RmlElementNode node, EmitContext context)
    {
        string builder = context.Builder;
        switch (assignment.Property)
        {
            case MarkupProperty.Color:
            {
                string? code = EmitColor(assignment, context);
                if (code == null)
                {
                    return;
                }

                context.Writer.Line(builder + ".Color = " + code + ";");
                break;
            }
            case MarkupProperty.BackgroundColor:
            {
                string? code = EmitColor(assignment, context);
                if (code == null)
                {
                    return;
                }

                if (!string.Equals(node.Tag, "Text", StringComparison.Ordinal)
                    && !string.Equals(node.Tag, "Image", StringComparison.Ordinal))
                {
                    context.Writer.Line(builder + ".Color = " + code + ";");
                }

                context.Writer.Line(builder + ".BackgroundColor = " + code + ";");
                break;
            }
            case MarkupProperty.TextColor:
            {
                string? code = EmitColor(assignment, context);
                if (code != null)
                {
                    context.Writer.Line(builder + ".TextColor = " + code + ";");
                }
                break;
            }
            case MarkupProperty.Padding:
            {
                int[] values = (int[])assignment.Value;
                context.Writer.Line(
                    builder + ".Padding = new global::Reef.UI.Padding(left: " + values[0]
                    + ", top: " + values[1] + ", right: " + values[2] + ", bottom: " + values[3] + ");");
                break;
            }
            case MarkupProperty.CornerRadius:
            {
                int[] values = (int[])assignment.Value;
                context.Writer.Line(
                    builder + ".CornerRadius = new global::Reef.UI.CornerRadius(topLeft: " + values[0]
                    + ", topRight: " + values[1] + ", bottomLeft: " + values[2] + ", bottomRight: " + values[3] + ");");
                break;
            }
            case MarkupProperty.Overlay:
            {
                bool? value = EmitBool(assignment, context);
                if (value != null)
                {
                    context.Writer.Line(builder + ".Overlay = " + (value.Value ? "true" : "false") + ";");
                }

                break;
            }
            case MarkupProperty.Layout:
            {
                MarkupLayoutDirection layout = (MarkupLayoutDirection)assignment.Value;
                string value = layout switch
                {
                    MarkupLayoutDirection.Vertical => "Vertical",
                    MarkupLayoutDirection.None => "None",
                    _ => "Horizontal",
                };
                context.Writer.Line(builder + ".LayoutDirection = global::Reef.UI.LayoutDirection." + value + ";");
                break;
            }
            case MarkupProperty.Spacing:
            {
                int? value = EmitInt(assignment, context);
                if (value != null)
                {
                    context.Writer.Line(builder + ".Spacing = " + value.Value + ";");
                }

                break;
            }
        }
    }

    private string? EmitColor(MarkupAssignment assignment, EmitContext context)
    {
        if (assignment.Value is MarkupExpression expression)
        {
            ITypeSymbol? colorType = TypeNames.GetVector4Type(Compilation);
            if (colorType == null)
            {
                return null;
            }

            return EmitTypedExpression(expression.Raw, colorType, assignment.Location, context.Scope);
        }

        return assignment.Value is System.Numerics.Vector4 color
            ? FnvHash.FormatVector(color)
            : null;
    }

    private int? EmitInt(MarkupAssignment assignment, EmitContext context)
    {
        if (assignment.Value is int value)
        {
            return value;
        }

        if (assignment.Value is MarkupExpression expression)
        {
            string? code = EmitTypedExpression(
                expression.Raw,
                Compilation.GetSpecialType(SpecialType.System_Int32),
                assignment.Location,
                context.Scope);
            if (code != null && int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out int literal))
            {
                return literal;
            }

            if (code != null)
            {
                //  Dynamic integer assignments need an expression; the caller
                //  currently emits only constants for these properties.
                return null;
            }
        }

        return null;
    }

    private bool? EmitBool(MarkupAssignment assignment, EmitContext context)
    {
        if (assignment.Value is bool value)
        {
            return value;
        }

        if (assignment.Value is MarkupExpression expression)
        {
            string? code = EmitTypedExpression(
                expression.Raw,
                Compilation.GetSpecialType(SpecialType.System_Boolean),
                assignment.Location,
                context.Scope);
            if (code == "true")
            {
                return true;
            }

            if (code == "false")
            {
                return false;
            }
        }

        return null;
    }

    private string? EmitConstraint(MarkupAssignment assignment, EmitContext context)
    {
        if (assignment.Value is MarkupConstraint constraint)
        {
            return BuildConstraint(constraint);
        }

        if (assignment.Value is MarkupExpression expression)
        {
            var binder = new ExpressionBinder(context.Scope);
            if (!binder.TryBind(expression.Raw, null, assignment.Location, out BoundExpression bound)
                || bound.Type == null)
            {
                return null;
            }

            ITypeSymbol? constraintType = Compilation.GetTypeByMetadataName("Reef.Constraints.IConstraint");
            if (constraintType != null && IsConstraintType(bound.Type, constraintType))
            {
                return bound.Code;
            }

            if (bound.Type.SpecialType == SpecialType.System_Int32)
            {
                return "new global::Reef.Constraints.Fixed(" + bound.Code + ")";
            }

            Report(
                DiagnosticDescriptors.TypeMismatch,
                assignment.Location,
                "Expected an integer or Reef.Constraints.IConstraint.");
        }

        return null;
    }

    private static bool IsConstraintType(ITypeSymbol type, ITypeSymbol constraintType)
    {
        if (TypeNames.IsSameType(type, constraintType))
        {
            return true;
        }

        for (var i = 0; i < type.AllInterfaces.Length; i++)
        {
            if (TypeNames.IsSameType(type.AllInterfaces[i], constraintType))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildConstraint(MarkupConstraint constraint)
    {
        switch (constraint.Kind)
        {
            case MarkupConstraintKind.Fill:
                return "new global::Reef.Constraints.Fill()";
            case MarkupConstraintKind.Fit:
                return "new global::Reef.Constraints.Fit()";
            case MarkupConstraintKind.Shrink:
                return "new global::Reef.Constraints.Shrink()";
            case MarkupConstraintKind.Relative:
                return "new global::Reef.UI.Relative(" + FnvHash.FormatFloat(constraint.Relative) + ")";
            default:
                return "new global::Reef.Constraints.Fixed(" + constraint.Fixed + ")";
        }
    }

    private string BuildAnchors(MarkupAnchors anchors)
    {
        if (anchors == MarkupAnchors.None)
        {
            return "default(global::Reef.UI.Anchors)";
        }

        var parts = new List<string>();
        if ((anchors & MarkupAnchors.Top) != 0)
        {
            parts.Add("global::Reef.UI.Anchors.Top");
        }

        if ((anchors & MarkupAnchors.Left) != 0)
        {
            parts.Add("global::Reef.UI.Anchors.Left");
        }

        if ((anchors & MarkupAnchors.Bottom) != 0)
        {
            parts.Add("global::Reef.UI.Anchors.Bottom");
        }

        if ((anchors & MarkupAnchors.Right) != 0)
        {
            parts.Add("global::Reef.UI.Anchors.Right");
        }

        if ((anchors & MarkupAnchors.Center) != 0)
        {
            parts.Add("global::Reef.UI.Anchors.Center");
        }

        if ((anchors & MarkupAnchors.Local) != 0)
        {
            parts.Add("global::Reef.UI.Anchors.Local");
        }

        return string.Join(" | ", parts);
    }

    private string? EmitElementId(RmlElementNode node, EmitContext context, bool needsId)
    {
        RmlAttribute? id = HasAttribute(node, "id");
        if (id != null)
        {
            return EmitStringExpression(id.Value, id.Location, context.Scope);
        }

        return needsId ? BuildDerivedId(node, context.IdPrefix) : null;
    }

    private string? EmitTextValue(RmlElementNode node, EmitContext context)
    {
        RmlAttribute? text = HasAttribute(node, "text");
        string raw;
        Location location;
        if (text != null)
        {
            raw = text.Value;
            location = text.Location;
        }
        else
        {
            raw = node.TextContent ?? string.Empty;
            location = node.Location;
            if (!node.PreserveWhitespace)
            {
                raw = raw.Trim();
            }
        }

        return EmitStringExpression(raw, location, context.Scope);
    }

    private string? EmitStringExpression(string raw, Location location, ExpressionScope scope)
    {
        string trimmed = raw.TrimStart();
        if (trimmed.StartsWith("@", StringComparison.Ordinal))
        {
            return EmitTypedExpression(raw, Compilation.GetSpecialType(SpecialType.System_String), location, scope);
        }

        if (raw.IndexOf('{') >= 0)
        {
            if (!BuildInterpolatedString(raw, location, out string interpolated))
            {
                return null;
            }

            return EmitTypedExpression(interpolated, Compilation.GetSpecialType(SpecialType.System_String), location, scope);
        }

        return FnvHash.EscapeString(raw);
    }

    private bool BuildInterpolatedString(string raw, Location location, out string csharp)
    {
        csharp = string.Empty;
        var builder = new System.Text.StringBuilder();
        builder.Append("$\"");
        for (var i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '{' && i + 1 < raw.Length && raw[i + 1] == '{')
            {
                builder.Append("{{");
                i++;
                continue;
            }

            if (c == '}' && i + 1 < raw.Length && raw[i + 1] == '}')
            {
                builder.Append("}}");
                i++;
                continue;
            }

            if (c == '{')
            {
                int end = FindMatchingBrace(raw, i + 1);
                if (end < 0)
                {
                    Report(DiagnosticDescriptors.InvalidExpression, location, raw, "The interpolation brace is not closed.");
                    return false;
                }

                builder.Append('{');
                builder.Append(raw, i + 1, end - i - 1);
                builder.Append('}');
                i = end;
                continue;
            }

            if (c == '"')
            {
                builder.Append("\\\"");
            }
            else if (c == '\\')
            {
                builder.Append("\\\\");
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
        }

        builder.Append('"');
        csharp = builder.ToString();
        return true;
    }

    private static int FindMatchingBrace(string text, int start)
    {
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'' || c == '"')
            {
                char quote = c;
                i++;
                while (i < text.Length)
                {
                    if (text[i] == '\\' && i + 1 < text.Length)
                    {
                        i += 2;
                        continue;
                    }

                    if (text[i] == quote)
                    {
                        break;
                    }

                    i++;
                }

                continue;
            }

            if (c == '{')
            {
                depth++;
                continue;
            }

            if (c == '}')
            {
                if (depth == 0)
                {
                    return i;
                }

                depth--;
            }
        }

        return -1;
    }

    private string? EmitTypedExpression(string raw, ITypeSymbol expected, Location location, ExpressionScope scope)
    {
        var binder = new ExpressionBinder(scope);
        return binder.TryBind(raw, expected, location, out BoundExpression bound) ? bound.Code : null;
    }

    private string? EmitBoolCondition(RmlElementNode node, string attributeName, EmitContext context, out bool literal)
    {
        literal = false;
        RmlAttribute? attribute = HasAttribute(node, attributeName);
        if (attribute == null)
        {
            return null;
        }

        string value = attribute.Value.Trim();
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            literal = true;
            return null;
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            literal = false;
            return "false";
        }

        return EmitTypedExpression(value, Compilation.GetSpecialType(SpecialType.System_Boolean), attribute.Location, context.Scope);
    }

    private static RmlAttribute? HasAttribute(RmlElementNode node, string name)
    {
        return node.FindAttribute(name);
    }

    private static string[] SplitClasses(string value)
    {
        return value.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
