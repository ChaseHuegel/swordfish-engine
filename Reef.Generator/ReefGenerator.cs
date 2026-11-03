using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Reef.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class ReefGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<AdditionalFileItem> rmlFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".rml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => new AdditionalFileItem(
                file.Path,
                file.GetText(cancellationToken)?.ToString() ?? string.Empty));

        IncrementalValuesProvider<AdditionalFileItem> rssFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".rss", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => new AdditionalFileItem(
                file.Path,
                file.GetText(cancellationToken)?.ToString() ?? string.Empty));

        IncrementalValuesProvider<RmlParseResult> parsedRml = rmlFiles
            .Select(static (file, cancellationToken) => new RmlParseResult(
                file.Path,
                file.Text,
                new RmlParser(file).Parse(cancellationToken)))
            .WithComparer(RmlParseResultComparer.Instance);

        IncrementalValuesProvider<RssParseResult> parsedRss = rssFiles
            .Select(static (file, cancellationToken) => new RssParseResult(
                file.Path,
                file.Text,
                new RssParser(file.Path, file.Text).Parse()))
            .WithComparer(RssParseResultComparer.Instance);

        IncrementalValueProvider<StyleOutput> styles = parsedRss
            .Collect()
            .Select(static (files, _) => BuildStyles(files))
            .WithComparer(StyleOutputComparer.Instance)
            .WithTrackingName("Styles");

        IncrementalValueProvider<RegistryOutput> registry = parsedRml
            .Collect()
            .Select(static (files, _) => BuildRegistry(files))
            .WithComparer(RegistryOutputComparer.Instance)
            .WithTrackingName("Components");

        context.RegisterSourceOutput(
            styles.Combine(registry).Select(static (pair, _) => pair.Left.Diagnostics.AddRange(pair.Right.Diagnostics)),
            static (productionContext, diagnostics) =>
            {
                for (var i = 0; i < diagnostics.Length; i++)
                {
                    productionContext.ReportDiagnostic(diagnostics[i]);
                }
            });

        IncrementalValuesProvider<EmitInput> documents = parsedRml
            .Combine(styles)
            .Combine(registry)
            .Combine(context.CompilationProvider)
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (tuple, _) => new EmitInput(
                tuple.Left.Left.Left.Left.Document,
                tuple.Left.Left.Left.Right.Catalog,
                tuple.Left.Left.Right.Registry,
                tuple.Left.Right,
                tuple.Right))
            .WithTrackingName("Documents");

        context.RegisterSourceOutput(documents, static (productionContext, input) =>
        {
            EmitDocument(productionContext, input);
        });
    }

    private static StyleOutput BuildStyles(ImmutableArray<RssParseResult> files)
    {
        var diagnostics = new List<Diagnostic>();
        var styleCatalog = new StyleResolver(files.Select(static file => file.File).ToList(), diagnostics).Resolve();
        styleCatalog.Validate();
        var signature = new StringBuilder();
        foreach (RssParseResult file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
        {
            signature.Append(file.Path).Append('\n').Append(file.Text).Append('\n');
        }

        return new StyleOutput(styleCatalog, diagnostics.ToImmutableArray(), signature.ToString());
    }

    private static RegistryOutput BuildRegistry(ImmutableArray<RmlParseResult> files)
    {
        var diagnostics = new List<Diagnostic>();
        var registry = new ComponentRegistry();
        for (var i = 0; i < files.Length; i++)
        {
            registry.Add(files[i].Document, diagnostics);
        }

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < files.Length; i++)
        {
            CollectComponentReferences(files[i].Document.Roots, registry, referenced);
        }

        var signature = new StringBuilder();
        foreach (RmlParseResult file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
        {
            if (referenced.Contains(file.Document.ClassName))
            {
                signature.Append(file.Path).Append('\n').Append(file.Text).Append('\n');
            }
        }

        return new RegistryOutput(registry, diagnostics.ToImmutableArray(), signature.ToString());
    }

    private static void CollectComponentReferences(
        IReadOnlyList<RmlNode> nodes,
        ComponentRegistry registry,
        HashSet<string> referenced)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            switch (nodes[i])
            {
                case RmlElementNode element:
                    if (registry.TryGet(element.Tag, out _))
                    {
                        referenced.Add(element.Tag);
                    }

                    CollectComponentReferences(element.Children, registry, referenced);
                    break;
                case RmlIfNode ifNode:
                    CollectComponentReferences(ifNode.Children, registry, referenced);
                    break;
                case RmlElseIfNode elseIfNode:
                    CollectComponentReferences(elseIfNode.Children, registry, referenced);
                    break;
                case RmlElseNode elseNode:
                    CollectComponentReferences(elseNode.Children, registry, referenced);
                    break;
                case RmlConditionalNode conditional:
                    for (var branch = 0; branch < conditional.Branches.Count; branch++)
                    {
                        CollectComponentReferences(conditional.Branches[branch].Children, registry, referenced);
                    }

                    break;
                case RmlForEachNode loop:
                    CollectComponentReferences(loop.Children, registry, referenced);
                    CollectComponentReferences(loop.EmptyChildren, registry, referenced);
                    break;
                case RmlContentNode content:
                    CollectComponentReferences(content.Children, registry, referenced);
                    break;
                case RmlEmptyNode empty:
                    CollectComponentReferences(empty.Children, registry, referenced);
                    break;
            }
        }
    }

    private static void EmitDocument(SourceProductionContext context, EmitInput input)
    {
        RmlDocument document = input.Document;
        for (var i = 0; i < document.Diagnostics.Count; i++)
        {
            context.ReportDiagnostic(document.Diagnostics[i]);
        }

        if (HasError(document.Diagnostics))
        {
            return;
        }

        try
        {
            var diagnostics = new List<Diagnostic>();
            string? source = DocumentEmitter.Emit(
                document,
                input.Compilation,
                input.Options,
                input.Styles,
                input.Registry,
                diagnostics);

            for (var i = 0; i < diagnostics.Count; i++)
            {
                context.ReportDiagnostic(diagnostics[i]);
            }

            if (source != null)
            {
                string hintName = BuildHintName(document);
                context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
            }
        }
        catch (Exception exception)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InternalError,
                document.ClassLocation,
                exception.Message));
        }
    }

    private static bool HasError(IReadOnlyList<Diagnostic> diagnostics)
    {
        for (var i = 0; i < diagnostics.Count; i++)
        {
            if (diagnostics[i].Severity == DiagnosticSeverity.Error)
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildHintName(RmlDocument document)
    {
        string fileName = Path.GetFileNameWithoutExtension(document.FilePath);
        if (fileName.Length == 0)
        {
            fileName = "Markup";
        }

        string safe = FnvHash.SanitizeIdentifier(fileName);
        return "ReefMarkup." + safe + "." + FnvHash.HashPath(document.FilePath) + ".g.cs";
    }

    private sealed class RmlParseResult
    {
        public readonly string Path;
        public readonly string Text;
        public readonly RmlDocument Document;

        public RmlParseResult(string path, string text, RmlDocument document)
        {
            Path = path;
            Text = text;
            Document = document;
        }
    }

    private sealed class RmlParseResultComparer : IEqualityComparer<RmlParseResult>
    {
        public static readonly RmlParseResultComparer Instance = new();

        public bool Equals(RmlParseResult? left, RmlParseResult? right)
        {
            return ReferenceEquals(left, right)
                   || (left != null && right != null
                       && string.Equals(left.Path, right.Path, StringComparison.Ordinal)
                       && string.Equals(left.Text, right.Text, StringComparison.Ordinal));
        }

        public int GetHashCode(RmlParseResult value)
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(value.Path) * 397)
                       ^ StringComparer.Ordinal.GetHashCode(value.Text);
            }
        }
    }

    private sealed class RssParseResult
    {
        public readonly string Path;
        public readonly string Text;
        public readonly RssFile File;

        public RssParseResult(string path, string text, RssFile file)
        {
            Path = path;
            Text = text;
            File = file;
        }
    }

    private sealed class RssParseResultComparer : IEqualityComparer<RssParseResult>
    {
        public static readonly RssParseResultComparer Instance = new();

        public bool Equals(RssParseResult? left, RssParseResult? right)
        {
            return ReferenceEquals(left, right)
                   || (left != null && right != null
                       && string.Equals(left.Path, right.Path, StringComparison.Ordinal)
                       && string.Equals(left.Text, right.Text, StringComparison.Ordinal));
        }

        public int GetHashCode(RssParseResult value)
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(value.Path) * 397)
                       ^ StringComparer.Ordinal.GetHashCode(value.Text);
            }
        }
    }

    private sealed class StyleOutput
    {
        public readonly StyleCatalog Catalog;
        public readonly ImmutableArray<Diagnostic> Diagnostics;
        public readonly string Signature;

        public StyleOutput(StyleCatalog catalog, ImmutableArray<Diagnostic> diagnostics, string signature)
        {
            Catalog = catalog;
            Diagnostics = diagnostics;
            Signature = signature;
        }
    }

    private sealed class StyleOutputComparer : IEqualityComparer<StyleOutput>
    {
        public static readonly StyleOutputComparer Instance = new();

        public bool Equals(StyleOutput? left, StyleOutput? right)
        {
            return ReferenceEquals(left, right)
                   || (left != null && right != null
                       && string.Equals(left.Signature, right.Signature, StringComparison.Ordinal));
        }

        public int GetHashCode(StyleOutput value)
        {
            return StringComparer.Ordinal.GetHashCode(value.Signature);
        }
    }

    private sealed class RegistryOutput
    {
        public readonly ComponentRegistry Registry;
        public readonly ImmutableArray<Diagnostic> Diagnostics;
        public readonly string Signature;

        public RegistryOutput(ComponentRegistry registry, ImmutableArray<Diagnostic> diagnostics, string signature)
        {
            Registry = registry;
            Diagnostics = diagnostics;
            Signature = signature;
        }
    }

    private sealed class RegistryOutputComparer : IEqualityComparer<RegistryOutput>
    {
        public static readonly RegistryOutputComparer Instance = new();

        public bool Equals(RegistryOutput? left, RegistryOutput? right)
        {
            return ReferenceEquals(left, right)
                   || (left != null && right != null
                       && string.Equals(left.Signature, right.Signature, StringComparison.Ordinal));
        }

        public int GetHashCode(RegistryOutput value)
        {
            return StringComparer.Ordinal.GetHashCode(value.Signature);
        }
    }

    private sealed class EmitInput
    {
        public readonly RmlDocument Document;
        public readonly StyleCatalog Styles;
        public readonly ComponentRegistry Registry;
        public readonly Compilation Compilation;
        public readonly AnalyzerConfigOptionsProvider Options;

        public EmitInput(
            RmlDocument document,
            StyleCatalog styles,
            ComponentRegistry registry,
            Compilation compilation,
            AnalyzerConfigOptionsProvider options)
        {
            Document = document;
            Styles = styles;
            Registry = registry;
            Compilation = compilation;
            Options = options;
        }
    }
}
