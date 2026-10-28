using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Reef.Generator;

internal sealed class RssParsedRule
{
    public string Selector = string.Empty;
    public string? ParentSelector;
    public RssPseudoState Pseudo;
    public readonly List<MarkupAssignment> Assignments = [];
    public Location Location = Location.None;
}

internal sealed class RssVariableValue
{
    public string Name = string.Empty;
    public List<RssToken> Tokens = [];
    public Location Location = Location.None;
}

internal sealed class StyleResolver
{
    private readonly List<RssFile> _files;
    private readonly List<Diagnostic> _diagnostics;
    private readonly Dictionary<string, List<RssToken>> _normalVariables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Location> _normalVariableLocations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<RssToken>> _overrideVariables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Location> _overrideVariableLocations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<RssToken>> _expandedVariables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<RssParsedRule>> _rulesByClass = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<MarkupAssignment>> _normalCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<RssPseudoState, List<MarkupAssignment>>> _pseudoCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _resolving = new(StringComparer.Ordinal);

    public StyleResolver(List<RssFile> files, List<Diagnostic> diagnostics)
    {
        _files = files;
        _diagnostics = diagnostics;
    }

    public StyleCatalog Resolve()
    {
        _files.Sort((left, right) => string.CompareOrdinal(left.FilePath, right.FilePath));
        for (var i = 0; i < _files.Count; i++)
        {
            RssFile file = _files[i];
            _diagnostics.AddRange(file.Diagnostics);
        }

        CollectVariables();
        ParseRules();
        return new StyleCatalog(this);
    }

    private void CollectVariables()
    {
        for (var i = 0; i < _files.Count; i++)
        {
            RssFile file = _files[i];
            for (var j = 0; j < file.Variables.Count; j++)
            {
                RssVariable variable = file.Variables[j];
                if (variable.Name.Length == 0)
                {
                    AddDiagnostic(
                        DiagnosticDescriptors.StyleSyntax,
                        variable.Location,
                        "A style variable must have a name.");
                    continue;
                }

                if (variable.IsOverride)
                {
                    if (_overrideVariables.ContainsKey(variable.Name))
                    {
                        AddDiagnostic(
                            DiagnosticDescriptors.ConflictingStyleOverride,
                            variable.Location,
                            variable.Name);
                        continue;
                    }

                    _overrideVariables[variable.Name] = Trim(variable.Value);
                    _overrideVariableLocations[variable.Name] = variable.Location;
                    continue;
                }

                if (_normalVariables.ContainsKey(variable.Name))
                {
                    AddDiagnostic(
                        DiagnosticDescriptors.DuplicateStyleVariable,
                        variable.Location,
                        variable.Name);
                    continue;
                }

                _normalVariables[variable.Name] = Trim(variable.Value);
                _normalVariableLocations[variable.Name] = variable.Location;
            }
        }

        var nameSet = new HashSet<string>(_normalVariables.Keys, StringComparer.Ordinal);
        nameSet.UnionWith(_overrideVariables.Keys);
        var names = new List<string>(nameSet);
        names.Sort(StringComparer.Ordinal);
        for (var i = 0; i < names.Count; i++)
        {
            string name = names[i];
            Location location = _overrideVariableLocations.TryGetValue(name, out Location overrideLocation)
                ? overrideLocation
                : _normalVariableLocations[name];
            var stack = new List<string>();
            if (!TryExpandVariable(name, stack, out List<RssToken> expanded, out string error))
            {
                AddDiagnostic(DiagnosticDescriptors.StyleSyntax, location, error);
                continue;
            }

            _expandedVariables[name] = expanded;
        }
    }

    private void ParseRules()
    {
        for (var i = 0; i < _files.Count; i++)
        {
            RssFile file = _files[i];
            for (var j = 0; j < file.Rules.Count; j++)
            {
                RssRule rule = file.Rules[j];
                var parsed = new RssParsedRule
                {
                    Selector = rule.Selector,
                    ParentSelector = rule.ParentSelector,
                    Pseudo = rule.Pseudo,
                    Location = rule.Location,
                };

                for (var k = 0; k < rule.Declarations.Count; k++)
                {
                    RssDeclaration declaration = rule.Declarations[k];
                    if (!TryExpandTokens(declaration.Value, declaration.Location, out List<RssToken> expanded, out string expansionError))
                    {
                        AddDiagnostic(DiagnosticDescriptors.StyleSyntax, declaration.Location, expansionError);
                        continue;
                    }

                    if (!StyleValueParser.TryParseStylesheet(declaration.Name, expanded, declaration.Location, out List<MarkupAssignment> assignments, out string parseError))
                    {
                        AddPropertyDiagnostic(declaration.Name, expanded, declaration.Location, parseError);
                        continue;
                    }

                    parsed.Assignments.AddRange(assignments);
                }

                if (!_rulesByClass.TryGetValue(parsed.Selector, out List<RssParsedRule> rules))
                {
                    rules = [];
                    _rulesByClass[parsed.Selector] = rules;
                }

                rules.Add(parsed);
            }
        }
    }

    private void AddPropertyDiagnostic(string property, IReadOnlyList<RssToken> tokens, Location location, string error)
    {
        string value = StyleValueParser.Describe(tokens);
        switch (property.ToLowerInvariant())
        {
            case "color":
            case "background":
            case "border-color":
            case "corner-caps-color":
                AddDiagnostic(DiagnosticDescriptors.InvalidColor, location, value);
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
                AddDiagnostic(DiagnosticDescriptors.InvalidConstraint, location, value, error);
                break;
            case "font-size":
                AddDiagnostic(DiagnosticDescriptors.InvalidDimension, location, value, error);
                break;
            case "padding":
            case "corner-radius":
            case "border":
            case "border-width":
            case "corner-caps":
            case "corner-caps-length":
            case "corner-caps-thickness":
                AddDiagnostic(DiagnosticDescriptors.InvalidShorthand, location, value, error);
                break;
            case "layout":
            case "text-align":
            case "vertical-align":
            case "white-space":
            case "anchor":
            case "anchors":
                AddDiagnostic(DiagnosticDescriptors.InvalidEnumValue, location, value, property);
                break;
            default:
                AddDiagnostic(DiagnosticDescriptors.UnknownStyleProperty, location, property);
                break;
        }
    }

    internal bool TryGetAssignments(string className, RssPseudoState state, out List<MarkupAssignment> assignments)
    {
        assignments = [];
        if (!_rulesByClass.ContainsKey(className))
        {
            return false;
        }

        if (state == RssPseudoState.None)
        {
            if (_normalCache.TryGetValue(className, out List<MarkupAssignment>? cached))
            {
                assignments = cached;
                return true;
            }

            var result = new List<MarkupAssignment>();
            ResolveClass(className, RssPseudoState.None, new HashSet<string>(StringComparer.Ordinal), result);
            _normalCache[className] = result;
            assignments = result;
            return true;
        }

        if (!_pseudoCache.TryGetValue(className, out Dictionary<RssPseudoState, List<MarkupAssignment>>? stateCache))
        {
            stateCache = [];
            _pseudoCache[className] = stateCache;
        }

        if (stateCache.TryGetValue(state, out List<MarkupAssignment>? cachedState))
        {
            assignments = cachedState;
            return true;
        }

        var stateResult = new List<MarkupAssignment>();
        ResolveClass(className, state, new HashSet<string>(StringComparer.Ordinal), stateResult);
        stateCache[state] = stateResult;
        assignments = stateResult;
        return true;
    }

    internal bool HasClass(string className)
    {
        return _rulesByClass.ContainsKey(className);
    }

    internal bool TryExpandTokens(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<RssToken> expanded,
        out string error)
    {
        expanded = [];
        error = string.Empty;
        for (var i = 0; i < tokens.Count; i++)
        {
            RssToken token = tokens[i];
            if (token.Kind != RssTokenKind.Identifier
                || !string.Equals(token.Text, "var", StringComparison.OrdinalIgnoreCase))
            {
                expanded.Add(token);
                continue;
            }

            int index = i + 1;
            SkipWhitespace(tokens, ref index);
            if (index >= tokens.Count || tokens[index].Kind != RssTokenKind.LParen)
            {
                error = "Expected '(' after var.";
                return false;
            }

            index++;
            SkipWhitespace(tokens, ref index);
            if (index >= tokens.Count || tokens[index].Kind != RssTokenKind.Variable)
            {
                error = "Expected a variable name inside var().";
                return false;
            }

            string name = tokens[index].Text.StartsWith("--", StringComparison.Ordinal)
                ? tokens[index].Text.Substring(2)
                : tokens[index].Text;
            index++;
            SkipWhitespace(tokens, ref index);
            if (index >= tokens.Count || tokens[index].Kind != RssTokenKind.RParen)
            {
                error = "Expected ')' after var().";
                return false;
            }

            if (!_expandedVariables.TryGetValue(name, out List<RssToken>? value))
            {
                error = "Style variable '--" + name + "' is not defined.";
                return false;
            }

            expanded.AddRange(value);
            i = index;
        }

        return true;
    }

    private bool TryExpandVariable(
        string name,
        List<string> stack,
        out List<RssToken> expanded,
        out string error)
    {
        expanded = [];
        error = string.Empty;
        if (stack.Contains(name))
        {
            error = "Style variable '--" + name + "' creates a circular reference.";
            return false;
        }

        stack.Add(name);
        List<RssToken> raw;
        if (_overrideVariables.TryGetValue(name, out List<RssToken>? overrideValue))
        {
            raw = overrideValue;
        }
        else if (!_normalVariables.TryGetValue(name, out raw!))
        {
            error = "Style variable '--" + name + "' is not defined.";
            stack.RemoveAt(stack.Count - 1);
            return false;
        }

        for (var i = 0; i < raw.Count; i++)
        {
            RssToken token = raw[i];
            if (token.Kind != RssTokenKind.Identifier
                || !string.Equals(token.Text, "var", StringComparison.OrdinalIgnoreCase))
            {
                expanded.Add(token);
                continue;
            }

            int index = i + 1;
            SkipWhitespace(raw, ref index);
            if (index >= raw.Count || raw[index].Kind != RssTokenKind.LParen)
            {
                error = "Expected '(' after var.";
                stack.RemoveAt(stack.Count - 1);
                return false;
            }

            index++;
            SkipWhitespace(raw, ref index);
            if (index >= raw.Count || raw[index].Kind != RssTokenKind.Variable)
            {
                error = "Expected a variable name inside var().";
                stack.RemoveAt(stack.Count - 1);
                return false;
            }

            string childName = raw[index].Text.StartsWith("--", StringComparison.Ordinal)
                ? raw[index].Text.Substring(2)
                : raw[index].Text;
            index++;
            SkipWhitespace(raw, ref index);
            if (index >= raw.Count || raw[index].Kind != RssTokenKind.RParen)
            {
                error = "Expected ')' after var().";
                stack.RemoveAt(stack.Count - 1);
                return false;
            }

            if (!TryExpandVariable(childName, stack, out List<RssToken> child, out error))
            {
                stack.RemoveAt(stack.Count - 1);
                return false;
            }

            expanded.AddRange(child);
            i = index;
        }

        stack.RemoveAt(stack.Count - 1);
        return true;
    }

    private void ResolveClass(
        string className,
        RssPseudoState state,
        HashSet<string> visiting,
        List<MarkupAssignment> result)
    {
        if (!visiting.Add(className))
        {
            AddDiagnostic(
                DiagnosticDescriptors.StyleInheritance,
                Location.None,
                "Circular style inheritance involving '." + className + "'.");
            return;
        }

        if (_rulesByClass.TryGetValue(className, out List<RssParsedRule>? rules))
        {
            var resolvedParents = new HashSet<string>(StringComparer.Ordinal);

            //  Inherit from every declared parent, regardless of which state
            //  declares the inheritance link. This lets .sub:hover inherit
            //  .base:hover even when only .sub : .base exists.
            for (var i = 0; i < rules.Count; i++)
            {
                RssParsedRule rule = rules[i];
                if (rule.ParentSelector == null || !resolvedParents.Add(rule.ParentSelector))
                {
                    continue;
                }

                if (!_rulesByClass.ContainsKey(rule.ParentSelector))
                {
                    AddDiagnostic(
                        DiagnosticDescriptors.StyleInheritance,
                        rule.Location,
                        "Style class '." + rule.ParentSelector + "' is not defined.");
                }
                else
                {
                    ResolveClass(rule.ParentSelector, state, visiting, result);
                }
            }

            for (var i = 0; i < rules.Count; i++)
            {
                RssParsedRule rule = rules[i];
                if (rule.Pseudo == state)
                {
                    result.AddRange(rule.Assignments);
                }
            }
        }

        visiting.Remove(className);
    }

    private void AddDiagnostic(DiagnosticDescriptor descriptor, Location location, params object[] args)
    {
        _diagnostics.Add(Diagnostic.Create(descriptor, location, args));
    }

    private static void SkipWhitespace(IReadOnlyList<RssToken> tokens, ref int index)
    {
        while (index < tokens.Count && tokens[index].Kind == RssTokenKind.Whitespace)
        {
            index++;
        }
    }

    private static List<RssToken> Trim(IReadOnlyList<RssToken> tokens)
    {
        var result = new List<RssToken>();
        int start = 0;
        int end = tokens.Count;
        while (start < end && tokens[start].Kind == RssTokenKind.Whitespace)
        {
            start++;
        }

        while (end > start && tokens[end - 1].Kind == RssTokenKind.Whitespace)
        {
            end--;
        }

        for (var i = start; i < end; i++)
        {
            result.Add(tokens[i]);
        }

        return result;
    }
}

internal sealed class StyleCatalog
{
    private readonly StyleResolver _resolver;

    public StyleCatalog(StyleResolver resolver)
    {
        _resolver = resolver;
    }

    public bool HasClass(string className)
    {
        return _resolver.HasClass(className);
    }

    public bool TryGetAssignments(string className, RssPseudoState state, out List<MarkupAssignment> assignments)
    {
        return _resolver.TryGetAssignments(className, state, out assignments);
    }

    public bool TryExpandTokens(IReadOnlyList<RssToken> tokens, Location location, out List<RssToken> expanded, out string error)
    {
        return _resolver.TryExpandTokens(tokens, location, out expanded, out error);
    }

    public bool TryExpandInlineValue(string value, Location location, out List<RssToken> tokens, out string error)
    {
        List<RssToken> raw = StyleValueParser.TokenizeValue(value);
        return _resolver.TryExpandTokens(raw, location, out tokens, out error);
    }
}
