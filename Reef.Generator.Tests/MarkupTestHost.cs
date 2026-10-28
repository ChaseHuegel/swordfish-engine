using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Reef;
using Reef.Text;
using Reef.UI;

namespace Reef.Generator.Tests;

internal sealed class MarkupTestHost
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    public static MarkupCompilation Compile(
        string markup,
        string companionCode = "",
        string? stylesheet = null,
        string fileName = "TestView.rml",
        string assemblyName = "ReefMarkupTest")
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var syntaxTrees = new List<SyntaxTree>();
        if (companionCode.Length > 0)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(companionCode, parseOptions, path: "Companion.cs"));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var additionalTexts = new List<AdditionalText>
        {
            new InMemoryAdditionalText(fileName, markup),
        };
        if (stylesheet != null)
        {
            additionalTexts.Add(new InMemoryAdditionalText("TestTheme.rss", stylesheet));
        }

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new ReefGenerator().AsSourceGenerator() },
            additionalTexts,
            parseOptions,
            new TestAnalyzerConfigOptionsProvider());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation updatedCompilation,
            out ImmutableArray<Diagnostic> driverDiagnostics);

        GeneratorDriverRunResult runResult = driver.GetRunResult();
        var generatedSources = new List<GeneratedSource>();
        foreach (GeneratorRunResult result in runResult.Results)
        {
            foreach (GeneratedSourceResult source in result.GeneratedSources)
            {
                generatedSources.Add(new GeneratedSource(
                    source.HintName,
                    source.SourceText.ToString()));
            }
        }

        var diagnostics = new List<Diagnostic>();
        diagnostics.AddRange(driverDiagnostics);
        diagnostics.AddRange(runResult.Diagnostics);
        diagnostics.AddRange(updatedCompilation.GetDiagnostics());

        return new MarkupCompilation(compilation, updatedCompilation, generatedSources, diagnostics);
    }

    public static MarkupCompilation CompileFiles(
        IEnumerable<(string Path, string Text)> files,
        string companionCode = "",
        string assemblyName = "ReefMarkupTest")
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var syntaxTrees = new List<SyntaxTree>();
        if (companionCode.Length > 0)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(companionCode, parseOptions, path: "Companion.cs"));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var additionalTexts = new List<AdditionalText>();
        foreach ((string path, string text) in files)
        {
            additionalTexts.Add(new InMemoryAdditionalText(path, text));
        }

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new ReefGenerator().AsSourceGenerator() },
            additionalTexts,
            parseOptions,
            new TestAnalyzerConfigOptionsProvider());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation updatedCompilation,
            out ImmutableArray<Diagnostic> driverDiagnostics);

        GeneratorDriverRunResult runResult = driver.GetRunResult();
        var generatedSources = new List<GeneratedSource>();
        foreach (GeneratorRunResult result in runResult.Results)
        {
            foreach (GeneratedSourceResult source in result.GeneratedSources)
            {
                generatedSources.Add(new GeneratedSource(source.HintName, source.SourceText.ToString()));
            }
        }

        var diagnostics = new List<Diagnostic>();
        diagnostics.AddRange(driverDiagnostics);
        diagnostics.AddRange(runResult.Diagnostics);
        diagnostics.AddRange(updatedCompilation.GetDiagnostics());
        var unique = diagnostics
            .GroupBy(diagnostic => (diagnostic.Id, diagnostic.Location, diagnostic.GetMessage()))
            .Select(group => group.First())
            .ToList();

        return new MarkupCompilation(compilation, updatedCompilation, generatedSources, unique);
    }

    public static Assembly EmitAssembly(MarkupCompilation result)
    {
        using var stream = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult emit = result.UpdatedCompilation.Emit(stream);
        if (!emit.Success)
        {
            var message = new StringBuilder();
            foreach (Diagnostic diagnostic in emit.Diagnostics)
            {
                message.AppendLine(diagnostic.ToString());
            }

            throw new InvalidOperationException(message.ToString());
        }

        return Assembly.Load(stream.ToArray());
    }

    public static object CreateContext(Assembly assembly, string typeName)
    {
        Type type = assembly.GetType(typeName, throwOnError: true)!;
        return Activator.CreateInstance(type)!;
    }

    public static List<RenderCommand<PixelTexture>> Render(
        Assembly assembly,
        string typeName,
        object context,
        string methodName = "Render")
    {
        Type type = assembly.GetType(typeName, throwOnError: true)!;
        object instance = Activator.CreateInstance(type)!;
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
                            ?? throw new InvalidOperationException("Render method not found.");

        var ui = new UIBuilder<PixelTexture>(320, 200, new StubTextEngine(), new UIController());
        if (method.IsGenericMethodDefinition)
        {
            method = method.MakeGenericMethod(typeof(PixelTexture));
        }

        object?[] arguments = method.GetParameters().Length == 5
            ? new object?[] { ui, context, null, null, null }
            : new object?[] { ui, context, null, null, null };
        method.Invoke(instance, arguments);
        return ui.Build(0d);
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var references = new List<MetadataReference>();
        string? trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (trusted != null)
        {
            foreach (string path in trusted.Split(Path.PathSeparator))
            {
                if (File.Exists(path))
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }

        references.Add(MetadataReference.CreateFromFile(typeof(UIBuilder<>).Assembly.Location));
        return references.ToImmutableArray();
    }
}

internal sealed class MarkupCompilation
{
    public readonly Compilation OriginalCompilation;
    public readonly Compilation UpdatedCompilation;
    public readonly IReadOnlyList<GeneratedSource> GeneratedSources;
    public readonly IReadOnlyList<Diagnostic> Diagnostics;

    public MarkupCompilation(
        Compilation originalCompilation,
        Compilation updatedCompilation,
        IReadOnlyList<GeneratedSource> generatedSources,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        OriginalCompilation = originalCompilation;
        UpdatedCompilation = updatedCompilation;
        GeneratedSources = generatedSources;
        Diagnostics = diagnostics;
    }

    public string GeneratedText => string.Join("\n", GeneratedSources.Select(source => source.Text));

    public IReadOnlyList<Diagnostic> Errors => Diagnostics
        .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        .ToList();
}

internal sealed record GeneratedSource(string HintName, string Text);

internal sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
{
    public override string Path => path;

    public override SourceText GetText(CancellationToken cancellationToken = default)
    {
        return SourceText.From(text);
    }
}

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly TestAnalyzerConfigOptions _options = new();

    public override AnalyzerConfigOptions GlobalOptions => _options;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal)
        {
            ["build_property.RootNamespace"] = "ReefMarkupTest",
            ["build_property.ProjectDir"] = Directory.GetCurrentDirectory(),
        };

        public override bool TryGetValue(string key, out string value)
        {
            return _values.TryGetValue(key, out value!);
        }
    }
}

internal sealed class StubTextEngine : ITextEngine
{
    public ITypeface GetDefaultTypeface() => null!;

    public ITypeface[] GetTypefaces() => [];

    public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
    {
        typeface = null;
        return false;
    }

    public TextConstraints Measure(FontOptions fontOptions, string text) => MeasureText(fontOptions, text);

    public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length)
    {
        return MeasureText(fontOptions, text.Substring(start, length));
    }

    public TextLayout Layout(FontOptions fontOptions, string text) => LayoutText(fontOptions, text);

    public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => LayoutText(fontOptions, text);

    public TextLayout Layout(FontOptions fontOptions, string text, int start, int length)
    {
        return LayoutText(fontOptions, text.Substring(start, length));
    }

    public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth)
    {
        return LayoutText(fontOptions, text.Substring(start, length));
    }

    private static TextConstraints MeasureText(FontOptions fontOptions, string text)
    {
        int width = Math.Max(0, text.Length) * 8;
        int height = Math.Max(1, fontOptions.Size);
        return new TextConstraints(width, height, width, height);
    }

    private static TextLayout LayoutText(FontOptions fontOptions, string text)
    {
        return new TextLayout(MeasureText(fontOptions, text), [], [], Math.Max(1, fontOptions.Size));
    }

    public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => [];

    public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth) => [];
}
