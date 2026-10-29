using Microsoft.CodeAnalysis;
using Xunit;

namespace Reef.Generator.Tests;

public class RssParserTests
{
    private static RssFile Parse(string css)
    {
        return new RssParser("Theme.rss", css).Parse();
    }

    [Fact]
    public void CommentsVariablesRulesAndPseudoStatesParse()
    {
        RssFile file = Parse("""
            /* theme */
            :root {
                --bg: #101010;
                --pad: 8 16;
            }

            .panel : .base {
                background: var(--bg);
                padding: var(--pad);
                border: 1 #444444;
            }

            .panel:hover {
                background: #202020;
            }
            """);
        Assert.Empty(file.Diagnostics);
        Assert.Equal(2, file.Variables.Count);
        Assert.Equal(2, file.Rules.Count);
        Assert.Equal("panel", file.Rules[0].Selector);
        Assert.Equal("base", file.Rules[0].ParentSelector);
        Assert.Equal(RssPseudoState.Hover, file.Rules[1].Pseudo);
    }

    [Fact]
    public void CssCompatibleExtendsAndImportantParse()
    {
        RssFile file = Parse("""
            :root {
                --accent: #ffaa00 !important;
            }

            .panel {
                --extends: .base;
                background: var(--accent);
            }
            """);
        Assert.Empty(file.Diagnostics);
        Assert.Single(file.Variables);
        Assert.True(file.Variables[0].IsOverride);
        Assert.Single(file.Rules);
        Assert.Equal("base", file.Rules[0].ParentSelector);
    }

    [Fact]
    public void OverrideAndDuplicateVariablesAreCaptured()
    {
        RssFile file = Parse("""
            :root {
                --a: #ff0000;
                override --b: #00ff00;
                --a: #0000ff;
            }
            """);
        Assert.Contains(file.Variables, variable => variable.Name == "b" && variable.IsOverride);
        var diagnostics = new List<Diagnostic>();
        StyleResolver resolver = new(new List<RssFile> { file }, diagnostics);
        resolver.Resolve();
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "RF0012");
    }

    [Fact]
    public void CircularVariableReportsRf0002()
    {
        RssFile file = Parse(":root { --a: var(--b); --b: var(--a); } .panel { color: var(--a); }");
        var diagnostics = new List<Diagnostic>();
        StyleCatalog catalog = new StyleResolver(new List<RssFile> { file }, diagnostics).Resolve();
        catalog.TryGetAssignments("panel", RssPseudoState.None, out _);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "RF0002");
    }

    [Fact]
    public void UndefinedVariableInVariableDefinitionReportsRf0002()
    {
        RssFile file = Parse(":root { --a: var(--missing); } .panel { color: var(--a); }");
        var diagnostics = new List<Diagnostic>();
        StyleCatalog catalog = new StyleResolver(new List<RssFile> { file }, diagnostics).Resolve();
        catalog.TryGetAssignments("panel", RssPseudoState.None, out _);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "RF0002");
    }

    [Fact]
    public void MalformedCssReportsRf0002()
    {
        RssFile file = Parse(".panel { color: #fff; ");
        var diagnostics = new List<Diagnostic>();
        StyleCatalog catalog = new StyleResolver(new List<RssFile> { file }, diagnostics).Resolve();
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "RF0002");
    }

    [Fact]
    public void UnknownPropertyReportsRf0022()
    {
        RssFile file = Parse(".panel { nonsense: 1; }");
        var diagnostics = new List<Diagnostic>();
        StyleCatalog catalog = new StyleResolver(new List<RssFile> { file }, diagnostics).Resolve();
        catalog.TryGetAssignments("panel", RssPseudoState.None, out _);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "RF0022");
    }

    [Fact]
    public void ColorsParseAllSupportedForms()
    {
        Assert.True(ColorParser.TryParse(StyleValueParser.TokenizeValue("#fff"), out var shortHex, out _));
        Assert.Equal(1f, shortHex.X, 3);
        Assert.True(ColorParser.TryParse(StyleValueParser.TokenizeValue("#ffffff80"), out var alphaHex, out _));
        Assert.Equal(0.5019608f, alphaHex.W, 3);
        Assert.True(ColorParser.TryParse(StyleValueParser.TokenizeValue("rebeccapurple"), out var named, out _));
        Assert.Equal(0.4f, named.X, 3);
        Assert.True(ColorParser.TryParse(StyleValueParser.TokenizeValue("rgb(0, 128, 255)"), out var rgb, out _));
        Assert.Equal(0.5019608f, rgb.Y, 3);
        Assert.True(ColorParser.TryParse(StyleValueParser.TokenizeValue("rgba(255, 255, 0, 0.5)"), out var rgba, out _));
        Assert.Equal(0.5f, rgba.W, 3);
    }

    [Fact]
    public void CssAliasesMapToReefProperties()
    {
        StyleValueParser.TryParseStylesheet(
            "border-radius",
            StyleValueParser.TokenizeValue("4px"),
            Location.None,
            out List<MarkupAssignment> radius,
            out _);
        Assert.Equal(MarkupProperty.CornerRadius, radius[0].Property);

        StyleValueParser.TryParseStylesheet(
            "gap",
            StyleValueParser.TokenizeValue("8px"),
            Location.None,
            out List<MarkupAssignment> gap,
            out _);
        Assert.Equal(MarkupProperty.Spacing, gap[0].Property);

        StyleValueParser.TryParseStylesheet(
            "background-color",
            StyleValueParser.TokenizeValue("#ff0000"),
            Location.None,
            out List<MarkupAssignment> background,
            out _);
        Assert.Equal(MarkupProperty.BackgroundColor, background[0].Property);
    }

    [Fact]
    public void PaddingAndCornerRadiusShorthandsMapToEngineFields()
    {
        StyleValueParser.TryParseStylesheet(
            "padding",
            StyleValueParser.TokenizeValue("10 20 30 40"),
            Location.None,
            out List<MarkupAssignment> padding,
            out _);
        int[] paddingValues = (int[])padding[0].Value;
        Assert.Equal(new[] { 10, 20, 30, 40 }, paddingValues);

        StyleValueParser.TryParseStylesheet(
            "corner-radius",
            StyleValueParser.TokenizeValue("4 8 12 16"),
            Location.None,
            out List<MarkupAssignment> radius,
            out _);
        int[] radiusValues = (int[])radius[0].Value;
        Assert.Equal(new[] { 4, 8, 16, 12 }, radiusValues);
    }

    [Fact]
    public void ConstraintsParseFixedRelativePercentageAndKeywords()
    {
        StyleValueParser.TryParseStylesheet(
            "width",
            StyleValueParser.TokenizeValue("100"),
            Location.None,
            out List<MarkupAssignment> fixedWidth,
            out _);
        Assert.Equal(MarkupConstraintKind.Fixed, ((MarkupConstraint)fixedWidth[0].Value).Kind);

        StyleValueParser.TryParseStylesheet(
            "width",
            StyleValueParser.TokenizeValue("50%"),
            Location.None,
            out List<MarkupAssignment> percentWidth,
            out _);
        Assert.Equal(0.5f, ((MarkupConstraint)percentWidth[0].Value).Relative, 3);

        StyleValueParser.TryParseStylesheet(
            "height",
            StyleValueParser.TokenizeValue("fill"),
            Location.None,
            out List<MarkupAssignment> fillHeight,
            out _);
        Assert.Equal(MarkupConstraintKind.Fill, ((MarkupConstraint)fillHeight[0].Value).Kind);
    }
}
