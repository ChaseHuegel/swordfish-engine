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
            .Select(static (file, cancellationToken) => new RmlParseResult(new RmlParser(file).Parse(cancellationToken)));

        IncrementalValuesProvider<RssFile> parsedRss = rssFiles
            .Select(static (file, cancellationToken) => new RssParser(file.Path, file.Text).Parse());

        IncrementalValueProvider<GenerationInput> input = parsedRml.Collect()
            .Combine(parsedRss.Collect())
            .Combine(context.CompilationProvider)
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (tuple, _) => new GenerationInput(
                tuple.Left.Left.Left,
                tuple.Left.Left.Right,
                tuple.Left.Right,
                tuple.Right));

        context.RegisterSourceOutput(input, static (productionContext, generationInput) =>
        {
            Execute(productionContext, generationInput);
        });
    }

    private static void Execute(SourceProductionContext context, GenerationInput input)
    {
        var allDiagnostics = new List<Diagnostic>();
        var styleDiagnostics = new List<Diagnostic>();
        var styleCatalog = new StyleResolver(input.RssFiles.ToList(), styleDiagnostics).Resolve();

        var registry = new ComponentRegistry();
        for (var i = 0; i < input.RmlFiles.Length; i++)
        {
            RmlDocument document = input.RmlFiles[i].Document;
            allDiagnostics.AddRange(document.Diagnostics);
            registry.Add(document, allDiagnostics);
        }

        for (var i = 0; i < input.RmlFiles.Length; i++)
        {
            RmlDocument document = input.RmlFiles[i].Document;
            if (HasError(document.Diagnostics))
            {
                continue;
            }

            try
            {
                var diagnostics = new List<Diagnostic>();
                string? source = DocumentEmitter.Emit(
                    document,
                    input.Compilation,
                    input.Options,
                    styleCatalog,
                    registry,
                    diagnostics);
                allDiagnostics.AddRange(diagnostics);

                if (source != null)
                {
                    string hintName = BuildHintName(document);
                    context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
                }
            }
            catch (Exception exception)
            {
                allDiagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.InternalError,
                    document.ClassLocation,
                    exception.Message));
            }
        }

        allDiagnostics.AddRange(styleDiagnostics);

        for (var i = 0; i < allDiagnostics.Count; i++)
        {
            context.ReportDiagnostic(allDiagnostics[i]);
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
        public readonly RmlDocument Document;

        public RmlParseResult(RmlDocument document)
        {
            Document = document;
        }
    }

    private sealed class GenerationInput
    {
        public readonly ImmutableArray<RmlParseResult> RmlFiles;
        public readonly ImmutableArray<RssFile> RssFiles;
        public readonly Compilation Compilation;
        public readonly AnalyzerConfigOptionsProvider Options;

        public GenerationInput(
            ImmutableArray<RmlParseResult> rmlFiles,
            ImmutableArray<RssFile> rssFiles,
            Compilation compilation,
            AnalyzerConfigOptionsProvider options)
        {
            RmlFiles = rmlFiles;
            RssFiles = rssFiles;
            Compilation = compilation;
            Options = options;
        }
    }
}
