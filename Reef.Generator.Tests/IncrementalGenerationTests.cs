using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Reef.Generator.Tests;

public class IncrementalGenerationTests
{
    [Fact]
    public void EditingOneDocumentRegeneratesOnlyThatDocument()
    {
        var page = new InMemoryAdditionalText("Page.rml", Markup("Page"));
        var other = new InMemoryAdditionalText("Other.rml", Markup("Other"));
        var theme = new InMemoryAdditionalText("Theme.rss", ".card { padding: 4px; }");
        CSharpCompilation compilation = MarkupTestHost.CreateCompilation();
        GeneratorDriver driver = CreateDriver([page, other, theme]);
        driver = driver.RunGenerators(compilation);

        var changed = new InMemoryAdditionalText("Page.rml", Markup("Page") + " ");
        driver = driver.ReplaceAdditionalText(page, changed);
        driver = driver.RunGenerators(compilation);

        GeneratorRunResult runResult = driver.GetRunResult().Results[0];
        ImmutableArray<IncrementalGeneratorRunStep> steps = runResult.TrackedSteps
            .Single(entry => entry.Key == "Documents")
            .Value;
        int rerun = steps.Count(step => step.Outputs.Any(output =>
            output.Reason is IncrementalStepRunReason.New or IncrementalStepRunReason.Modified));
        Assert.Equal(1, rerun);
    }

    [Fact]
    public void EditingOneStylesheetRerunsTheStyleStepOnce()
    {
        var page = new InMemoryAdditionalText("Page.rml", Markup("Page"));
        var theme = new InMemoryAdditionalText("Theme.rss", ".card { padding: 4px; }");
        CSharpCompilation compilation = MarkupTestHost.CreateCompilation();
        GeneratorDriver driver = CreateDriver([page, theme]);
        driver = driver.RunGenerators(compilation);

        var changed = new InMemoryAdditionalText("Theme.rss", ".card { padding: 8px; }");
        driver = driver.ReplaceAdditionalText(theme, changed);
        driver = driver.RunGenerators(compilation);

        ImmutableArray<IncrementalGeneratorRunStep> steps = driver.GetRunResult()
            .Results[0]
            .TrackedSteps
            .Single(entry => entry.Key == "Styles")
            .Value;
        Assert.Single(steps);
        Assert.Contains(steps[0].Outputs, output =>
            output.Reason is IncrementalStepRunReason.New or IncrementalStepRunReason.Modified);
    }

    private static GeneratorDriver CreateDriver(IEnumerable<AdditionalText> texts)
    {
        return CSharpGeneratorDriver.Create(
            [new ReefGenerator().AsSourceGenerator()],
            texts,
            CSharpParseOptions.Default,
            new TestAnalyzerConfigOptionsProvider(),
            new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    }

    private static string Markup(string className)
    {
        return "<Reef xmlns=\"Reef\" Class=\"" + className
               + "\" Namespace=\"ReefIncrementalTest\"><Element /></Reef>";
    }
}
