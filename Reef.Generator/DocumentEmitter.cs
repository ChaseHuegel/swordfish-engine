using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace Reef.Generator;

internal static class DocumentEmitter
{
    public static string? Emit(
        RmlDocument document,
        Compilation compilation,
        AnalyzerConfigOptionsProvider options,
        StyleCatalog styleCatalog,
        ComponentRegistry registry,
        List<Diagnostic> diagnostics)
    {
        var state = new DocumentState(
            document,
            compilation,
            options,
            styleCatalog,
            registry,
            diagnostics);
        return state.Emit();
    }
}

internal sealed partial class DocumentState
{
    private readonly RmlDocument _document;
    private readonly Compilation _compilation;
    private readonly AnalyzerConfigOptionsProvider _options;
    private readonly StyleCatalog _styleCatalog;
    private readonly ComponentRegistry _registry;
    private readonly List<Diagnostic> _diagnostics;
    private readonly List<MarkupHelper> _helpers = [];
    private readonly List<SlotInfo> _slots = [];
    private readonly Dictionary<RmlDocument, ITypeSymbol?> _childContextTypes = [];
    private readonly Dictionary<RmlDocument, ITypeSymbol?> _childParameterTypes = [];
    private readonly HashSet<string> _inlineStack = new(StringComparer.Ordinal);
    private readonly HashSet<string> _declaredHelperKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _existingMethodKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _prefixNamespaces = new(StringComparer.Ordinal);
    private ExpressionScope _rootScope = null!;
    private string _className = string.Empty;
    private string _namespaceName = string.Empty;
    private string _access = "public";
    private INamedTypeSymbol? _companionType;
    private ITypeSymbol? _contextType;
    private ITypeSymbol? _parameterType;
    private ITypeSymbol? _rendererType;
    private ITypeSymbol? _builderType;
    private int _localCounter;

    public DocumentState(
        RmlDocument document,
        Compilation compilation,
        AnalyzerConfigOptionsProvider options,
        StyleCatalog styleCatalog,
        ComponentRegistry registry,
        List<Diagnostic> diagnostics)
    {
        _document = document;
        _compilation = compilation;
        _options = options;
        _styleCatalog = styleCatalog;
        _registry = registry;
        _diagnostics = diagnostics;
    }

    public string? Emit()
    {
        if (!ValidateRoot())
        {
            return null;
        }

        CollectSlots();
        CollectPrefixNamespaces();
        BuildRootScope();
        ValidateDuplicateIds(_document.Roots, new Dictionary<string, Location>(StringComparer.Ordinal));

        var body = new CodeWriter();
        var rootContext = CreateContext(
            body,
            _rootScope,
            builder: "ui",
            idPrefix: "idPrefix",
            isRoot: true,
            callerRuntime: true,
            callerPlan: null,
            slots: null);
        EmitNodes(_document.Roots, rootContext);

        var declarations = new CodeWriter();
        EmitHelperDeclarations(declarations);

        var writer = new CodeWriter();
        EmitHeader(writer);
        EmitComponentAttribute(writer);
        EmitClassStart(writer);
        EmitHelperDeclarations(writer, declarations);
        EmitRenderMethod(writer, body.ToString());
        EmitClassEnd(writer);
        return writer.ToString();
    }

    private bool ValidateRoot()
    {
        _className = _document.ClassName.Trim();
        if (!TypeResolver.IsValidIdentifier(_className))
        {
            Report(DiagnosticDescriptors.InvalidRoot, _document.ClassLocation, "Class must be a valid C# identifier.");
            return false;
        }

        _namespaceName = TypeResolver.InferNamespace(_document, _compilation, _options).Trim();
        if (!TypeResolver.IsValidNamespace(_namespaceName))
        {
            Report(DiagnosticDescriptors.InvalidRoot, _document.ClassLocation, "Namespace is not valid C#.");
            return false;
        }

        string fullName = _namespaceName.Length > 0 ? _namespaceName + "." + _className : _className;
        _companionType = _compilation.GetTypeByMetadataName(fullName);
        if (_companionType != null && !IsSourcePartial(_companionType))
        {
            Report(
                DiagnosticDescriptors.InvalidRoot,
                _document.ClassLocation,
                "A non-partial type named '" + fullName + "' already exists in this compilation.");
            return false;
        }

        if (_document.Access != null)
        {
            string requested = _document.Access.Trim().ToLowerInvariant();
            if (requested != "public" && requested != "internal")
            {
                Report(DiagnosticDescriptors.InvalidEnumValue, _document.ClassLocation, _document.Access, "Access");
                return false;
            }

            _access = requested;
        }
        else if (_companionType != null && _companionType.DeclaredAccessibility == Accessibility.Internal)
        {
            _access = "internal";
        }
        else
        {
            _access = "public";
        }

        if (_document.ContextType != null && _document.ContextType.Trim().Length > 0)
        {
            _contextType = TypeResolver.ResolveType(_compilation, _document.ContextType, _namespaceName);
            if (_contextType == null)
            {
                Report(
                    DiagnosticDescriptors.InvalidRoot,
                    _document.ClassLocation,
                    "Context type '" + _document.ContextType + "' was not found.");
                return false;
            }

            if (!TypeResolver.IsAccessible(_compilation, _contextType))
            {
                Report(
                    DiagnosticDescriptors.InvalidRoot,
                    _document.ClassLocation,
                    "Context type '" + _document.ContextType + "' is not accessible.");
                return false;
            }

            if (_access == "public" && _contextType.DeclaredAccessibility != Accessibility.Public)
            {
                Report(
                    DiagnosticDescriptors.InvalidRoot,
                    _document.ClassLocation,
                    "A public view cannot expose a non-public context type.");
                return false;
            }
        }

        if (_document.ParameterType != null && _document.ParameterType.Trim().Length > 0)
        {
            _parameterType = TypeResolver.ResolveType(_compilation, _document.ParameterType, _namespaceName);
            if (_parameterType == null)
            {
                Report(
                    DiagnosticDescriptors.InvalidRoot,
                    _document.ClassLocation,
                    "Parameter type '" + _document.ParameterType + "' was not found.");
                return false;
            }

            if (!TypeResolver.IsAccessible(_compilation, _parameterType))
            {
                Report(
                    DiagnosticDescriptors.InvalidRoot,
                    _document.ClassLocation,
                    "Parameter type '" + _document.ParameterType + "' is not accessible.");
                return false;
            }
        }

        if (_document.RendererDataType != null && _document.RendererDataType.Trim().Length > 0)
        {
            _rendererType = TypeResolver.ResolveType(_compilation, _document.RendererDataType, _namespaceName);
            if (_rendererType == null)
            {
                Report(
                    DiagnosticDescriptors.InvalidRoot,
                    _document.ClassLocation,
                    "RendererData type '" + _document.RendererDataType + "' was not found.");
                return false;
            }

            if (!TypeResolver.IsAccessible(_compilation, _rendererType))
            {
                Report(
                    DiagnosticDescriptors.InvalidRoot,
                    _document.ClassLocation,
                    "RendererData type '" + _document.RendererDataType + "' is not accessible.");
                return false;
            }

            if (_access == "public" && _rendererType.DeclaredAccessibility != Accessibility.Public)
            {
                Report(
                    DiagnosticDescriptors.InvalidRoot,
                    _document.ClassLocation,
                    "A public view cannot expose a non-public renderer data type.");
                return false;
            }
        }

        _builderType = TypeNames.GetUIBuilderType(_compilation, _rendererType);
        if (_builderType == null)
        {
            Report(
                DiagnosticDescriptors.UnresolvedExpression,
                _document.ClassLocation,
                "Reef.UIBuilder<T> was not found. Reference the Reef assembly.");
            return false;
        }

        if (_companionType != null)
        {
            foreach (ISymbol member in _companionType.GetMembers())
            {
                if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                {
                    //  A companion partial implementation still needs the
                    //  generator to emit the defining declaration.
                    if (CompanionMethods.IsPartialImplementation(method))
                    {
                        continue;
                    }

                    _existingMethodKeys.Add(HelperSignatureKey(method));
                }
            }
        }

        return true;
    }

    private bool IsSourcePartial(INamedTypeSymbol type)
    {
        if (type.DeclaringSyntaxReferences.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < type.DeclaringSyntaxReferences.Length; i++)
        {
            SyntaxNode node = type.DeclaringSyntaxReferences[i].GetSyntax();
            if (node is not Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax declaration)
            {
                return false;
            }

            bool partial = false;
            foreach (SyntaxToken modifier in declaration.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                {
                    partial = true;
                    break;
                }
            }

            if (!partial)
            {
                return false;
            }
        }

        return true;
    }

    private void ValidateDuplicateIds(IReadOnlyList<RmlNode> nodes, Dictionary<string, Location> ids)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            switch (nodes[i])
            {
                case RmlElementNode element:
                    RmlAttribute? id = HasAttribute(element, "id");
                    if (id != null
                        && id.Value.IndexOf('{') < 0
                        && !id.Value.TrimStart().StartsWith("@", StringComparison.Ordinal))
                    {
                        string value = id.Value.Trim();
                        if (ids.TryGetValue(value, out Location? previous))
                        {
                            Report(DiagnosticDescriptors.DuplicateElementId, id.Location, value);
                        }
                        else
                        {
                            ids[value] = id.Location;
                        }
                    }

                    ValidateDuplicateIds(element.Children, ids);
                    break;
                case RmlConditionalNode conditional:
                    for (var j = 0; j < conditional.Branches.Count; j++)
                    {
                        ValidateDuplicateIds(conditional.Branches[j].Children, ids);
                    }

                    break;
                case RmlForEachNode forEach:
                    ValidateDuplicateIds(forEach.Children, ids);
                    ValidateDuplicateIds(forEach.EmptyChildren, ids);
                    break;
                case RmlContentNode content:
                    ValidateDuplicateIds(content.Children, ids);
                    break;
            }
        }
    }

    private void CollectSlots()
    {
        var names = new Dictionary<string, SlotInfo>(StringComparer.Ordinal);
        var ordered = new List<SlotInfo>();
        CollectSlots(_document.Roots, names, ordered);
        _slots.AddRange(ordered);
    }

    private void CollectSlots(IReadOnlyList<RmlNode> nodes, Dictionary<string, SlotInfo> slots, List<SlotInfo> ordered)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            switch (nodes[i])
            {
                case RmlContentNode content:
                    string name = string.IsNullOrWhiteSpace(content.Name) ? "default" : content.Name!.Trim();
                    if (!string.Equals(name, "default", StringComparison.Ordinal)
                        && !TypeResolver.IsValidIdentifier(name))
                    {
                        Report(
                            DiagnosticDescriptors.SlotMismatch,
                            content.Location,
                            "Slot name '" + name + "' must be a valid C# identifier.");
                        break;
                    }

                    if (slots.TryGetValue(name, out SlotInfo? existing))
                    {
                        Report(
                            DiagnosticDescriptors.SlotMismatch,
                            content.Location,
                            "Slot '" + name + "' is declared more than once.");
                        existing.HasDefault |= content.Children.Count > 0;
                        break;
                    }

                    var slotInfo = new SlotInfo
                    {
                        Name = name,
                        ParameterName = "content_" + FnvHash.SanitizeIdentifier(name),
                        HasDefault = content.Children.Count > 0,
                        Location = content.Location,
                    };
                    slots[name] = slotInfo;
                    ordered.Add(slotInfo);
                    CollectSlots(content.Children, slots, ordered);
                    break;
                case RmlElementNode element:
                    if (element.Prefix.Length == 0 && _registry.TryGet(element.Tag, out _))
                    {
                        break;
                    }

                    if (element.Prefix.Length > 0)
                    {
                        break;
                    }

                    if (!MarkupVocabulary.IsBuiltInTag(element.Tag)
                        && HasReferencedComponent(element.Tag))
                    {
                        break;
                    }

                    CollectSlots(element.Children, slots, ordered);
                    break;
                case RmlConditionalNode conditional:
                    for (var j = 0; j < conditional.Branches.Count; j++)
                    {
                        CollectSlots(conditional.Branches[j].Children, slots, ordered);
                    }

                    break;
                case RmlForEachNode forEach:
                    CollectSlots(forEach.Children, slots, ordered);
                    CollectSlots(forEach.EmptyChildren, slots, ordered);
                    break;
            }
        }
    }

    private void CollectPrefixNamespaces()
    {
        foreach (KeyValuePair<string, string> pair in _document.NamespacePrefixes)
        {
            if (pair.Key.Length == 0
                || string.Equals(pair.Key, "xml", StringComparison.Ordinal)
                || string.Equals(pair.Key, "xsi", StringComparison.Ordinal)
                || string.Equals(pair.Key, "class", StringComparison.Ordinal)
                || string.Equals(pair.Value, "Reef", StringComparison.Ordinal)
                || string.Equals(pair.Value, "Reef.Class", StringComparison.Ordinal))
            {
                continue;
            }

            _prefixNamespaces[pair.Key] = pair.Value;
        }
    }

    private void BuildRootScope()
    {
        var variables = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);
        if (_contextType != null)
        {
            variables["context"] = _contextType;
        }

        if (_parameterType != null)
        {
            variables["param"] = _parameterType;
        }

        _rootScope = new ExpressionScope(
            _compilation,
            _companionType,
            _contextType,
            _rendererType,
            contextExpression: _contextType != null ? "context" : string.Empty,
            builderExpression: "ui",
            variables,
            _diagnostics,
            _helpers);
    }

    private EmitContext CreateContext(
        CodeWriter writer,
        ExpressionScope scope,
        string builder,
        string idPrefix,
        bool isRoot,
        bool callerRuntime,
        CallerPlan? callerPlan,
        Dictionary<string, SlotContent>? slots)
    {
        return new EmitContext
        {
            State = this,
            Writer = writer,
            Scope = scope,
            Builder = builder,
            IdPrefix = idPrefix,
            IsRoot = isRoot,
            CallerRuntime = callerRuntime,
            CallerPlan = callerPlan,
            Slots = slots,
            LoopDepth = 0,
        };
    }

    private void EmitHeader(CodeWriter writer)
    {
        writer.Line("// <auto-generated by Reef.Generator />");
        writer.Line("#nullable enable");
        writer.Line("using System;");
        writer.Line("using System.Numerics;");
        writer.Line("using Reef;");
        writer.Line("using Reef.Constraints;");
        writer.Line("using Reef.Text;");
        writer.Line("using Reef.UI;");
        foreach (string ns in _prefixNamespaces.Values.Distinct(StringComparer.Ordinal))
        {
            writer.Line("using " + ns + ";");
        }

        if (_namespaceName.Length > 0)
        {
            writer.Line();
            writer.Line("namespace " + _namespaceName + ";");
        }

        writer.Line();
    }

    private void EmitComponentAttribute(CodeWriter writer)
    {
        bool hasAttribute = false;
        if (_companionType != null)
        {
            foreach (AttributeData attribute in _companionType.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == "Reef.ReefComponentAttribute")
                {
                    hasAttribute = true;
                    break;
                }
            }
        }

        if (hasAttribute)
        {
            return;
        }

        string context = _contextType != null
            ? ", typeof(" + TypeNames.Display(_contextType) + ")"
            : string.Empty;
        writer.Line("[global::Reef.ReefComponent(" + FnvHash.EscapeString(_className) + context + ")]");
    }

    private void EmitClassStart(CodeWriter writer)
    {
        writer.Line(_access + " partial class " + _className);
        writer.Open();
    }

    private void EmitClassEnd(CodeWriter writer)
    {
        writer.Close();
    }

    private void EmitHelperDeclarations(CodeWriter writer, CodeWriter declarations)
    {
        string text = declarations.ToString();
        if (text.Length > 0)
        {
            foreach (string line in text.Split('\n'))
            {
                if (line.Length > 0)
                {
                    writer.RawLine("    " + line);
                }
            }

            writer.Line();
        }
    }

    private void EmitHelperDeclarations(CodeWriter writer)
    {
        for (var i = 0; i < _helpers.Count; i++)
        {
            MarkupHelper helper = _helpers[i];
            string key = helper.SignatureKey();
            if (_existingMethodKeys.Contains(key) || !_declaredHelperKeys.Add(key))
            {
                continue;
            }

            var parameters = new List<string>();
            for (var j = 0; j < helper.Parameters.Count; j++)
            {
                string parameterName = j < helper.ParameterNames.Count
                    ? helper.ParameterNames[j]
                    : "arg" + j;
                parameters.Add(TypeNames.Display(helper.Parameters[j]) + " " + parameterName);
            }

            string returnType = TypeNames.Display(helper.ReturnType);
            if (helper.IsCallback || returnType == "void")
            {
                writer.Line("partial void " + helper.Name + "(" + string.Join(", ", parameters) + ");");
            }
            else
            {
                writer.Line("protected partial " + returnType + " " + helper.Name + "(" + string.Join(", ", parameters) + ");");
            }
        }
    }

    private void EmitRenderMethod(CodeWriter writer, string body)
    {
        string builderType = BuildBuilderType();
        var parameters = new List<string>
        {
            builderType + " ui",
        };

        if (_contextType != null)
        {
            parameters.Add(TypeNames.Display(_contextType) + " context");
        }

        if (_parameterType != null)
        {
            parameters.Add(TypeNames.Display(_parameterType) + "? parameters = null");
        }

        parameters.Add("global::Reef.UI.StyleOverride? callerStyle = null");
        parameters.Add("global::Reef.UI.ConstraintsOverride? callerConstraints = null");
        parameters.Add("string? idPrefix = null");

        for (var i = 0; i < _slots.Count; i++)
        {
            SlotInfo slot = _slots[i];
            parameters.Add(
                "global::System.Action<" + builderType + ">? " + slot.ParameterName + " = null");
        }

        string generic = _rendererType == null ? "<TData>" : string.Empty;
        writer.Line("public void Render" + generic + "(" + string.Join(", ", parameters) + ")");
        writer.Open();
        if (_parameterType != null)
        {
            writer.Line("var param = parameters ?? new " + TypeNames.Display(_parameterType) + "();");
        }
        EmitBodyLines(writer, body);
        writer.Close();
    }

    private static void EmitBodyLines(CodeWriter writer, string body)
    {
        string[] lines = body.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            writer.RawLine("        " + line);
        }
    }

    private string BuildBuilderType()
    {
        return _rendererType != null
            ? "global::Reef.UIBuilder<" + TypeNames.Display(_rendererType) + ">"
            : "global::Reef.UIBuilder<TData>";
    }

    private string HelperSignatureKey(IMethodSymbol method)
    {
        var parts = new List<string> { method.Name };
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            parts.Add(TypeNames.Display(method.Parameters[i].Type));
        }

        return string.Join("|", parts);
    }

    internal ITypeSymbol? GetChildContextType(RmlDocument document)
    {
        if (_childContextTypes.TryGetValue(document, out ITypeSymbol? cached))
        {
            return cached;
        }

        if (document.ContextType == null || document.ContextType.Trim().Length == 0)
        {
            _childContextTypes[document] = null;
            return null;
        }

        string ns = TypeResolver.InferNamespace(document, _compilation, _options);
        ITypeSymbol? type = TypeResolver.ResolveType(_compilation, document.ContextType, ns);
        _childContextTypes[document] = type;
        return type;
    }

    internal ITypeSymbol? GetChildParameterType(RmlDocument document)
    {
        if (_childParameterTypes.TryGetValue(document, out ITypeSymbol? cached))
        {
            return cached;
        }

        if (document.ParameterType == null || document.ParameterType.Trim().Length == 0)
        {
            _childParameterTypes[document] = null;
            return null;
        }

        string ns = TypeResolver.InferNamespace(document, _compilation, _options);
        ITypeSymbol? type = TypeResolver.ResolveType(_compilation, document.ParameterType, ns);
        _childParameterTypes[document] = type;
        return type;
    }

    internal bool IsLocalComponent(string tag)
    {
        return _registry.TryGet(tag, out _);
    }

    internal bool TryGetLocalComponent(string tag, out RmlDocument document)
    {
        return _registry.TryGet(tag, out document!);
    }

    internal StyleCatalog StyleCatalog => _styleCatalog;

    internal Compilation Compilation => _compilation;

    internal ITypeSymbol? ContextType => _contextType;

    internal ITypeSymbol? RendererType => _rendererType;

    internal ITypeSymbol? BuilderType => _builderType;

    internal List<MarkupHelper> Helpers => _helpers;

    internal List<Diagnostic> Diagnostics => _diagnostics;

    internal HashSet<string> InlineStack => _inlineStack;

    internal MarkupHelper RegisterCallback(string name, ITypeSymbol parameterType, Location location)
    {
        var parameters = new List<ITypeSymbol> { parameterType };
        for (var i = 0; i < _helpers.Count; i++)
        {
            MarkupHelper existing = _helpers[i];
            if (string.Equals(existing.Name, name, StringComparison.Ordinal)
                && existing.Parameters.Count == 1
                && TypeNames.IsSameType(existing.Parameters[0], parameterType))
            {
                return existing;
            }
        }

        var helper = new MarkupHelper
        {
            Name = name,
            ReturnType = _compilation.GetSpecialType(SpecialType.System_Void),
            IsCallback = true,
            Location = location,
        };
        helper.Parameters.Add(parameterType);
        if (_companionType != null)
        {
            foreach (ISymbol member in _companionType.GetMembers(name))
            {
                if (member is IMethodSymbol method && method.Parameters.Length == 1)
                {
                    helper.ParameterNames.Add(method.Parameters[0].Name);
                    break;
                }
            }
        }

        _helpers.Add(helper);
        return helper;
    }

    internal string NextLocal(string prefix)
    {
        _localCounter++;
        return prefix + _localCounter;
    }

    internal void Report(DiagnosticDescriptor descriptor, Location location, params object[] args)
    {
        _diagnostics.Add(Diagnostic.Create(descriptor, location, args));
    }

    internal string BuildDerivedId(RmlNode node, string idPrefix)
    {
        string key = GetFileKey();
        string literal = "\"__rml_" + key + "_" + node.Line + "_" + node.Column + "\"";
        if (string.IsNullOrEmpty(idPrefix))
        {
            return literal;
        }

        return "(" + idPrefix + " + " + literal + ")";
    }

    internal string GetFileKey()
    {
        string path = _document.FilePath;
        _options.GlobalOptions.TryGetValue("build_property.ProjectDir", out string? projectDirectory);
        if (!string.IsNullOrWhiteSpace(projectDirectory))
        {
            try
            {
                string fullBase = System.IO.Path.GetFullPath(projectDirectory!.Trim())
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
                    + System.IO.Path.DirectorySeparatorChar;
                string fullPath = System.IO.Path.GetFullPath(path);
                if (fullPath.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
                {
                    path = fullPath.Substring(fullBase.Length);
                }
            }
            catch (ArgumentException)
            {
                //  Keep the absolute path.
            }
        }

        return FnvHash.HashPath(path.Replace('\\', '/'));
    }

    internal string CombineIdPrefix(string parent, string child)
    {
        if (string.IsNullOrEmpty(parent))
        {
            return child;
        }

        if (string.IsNullOrEmpty(child))
        {
            return parent;
        }

        return "(" + parent + " + " + child + ")";
    }
}

internal sealed class EmitContext
{
    public DocumentState State = null!;
    public CodeWriter Writer = null!;
    public ExpressionScope Scope = null!;
    public string Builder = "ui";
    public string IdPrefix = string.Empty;
    public bool IsRoot;
    public bool CallerRuntime;
    public CallerPlan? CallerPlan;
    public Dictionary<string, SlotContent>? Slots;
    public int LoopDepth;
    public readonly List<string> LoopIndexes = [];
    public readonly List<string> LoopItems = [];
    public bool RuntimeCallerApplied;

    public EmitContext Clone(ExpressionScope scope)
    {
        var clone = new EmitContext
        {
            State = State,
            Writer = Writer,
            Scope = scope,
            Builder = Builder,
            IdPrefix = IdPrefix,
            IsRoot = false,
            CallerRuntime = false,
            CallerPlan = CallerPlan,
            Slots = Slots,
            LoopDepth = LoopDepth,
        };
        clone.LoopIndexes.AddRange(LoopIndexes);
        clone.LoopItems.AddRange(LoopItems);
        return clone;
    }
}
