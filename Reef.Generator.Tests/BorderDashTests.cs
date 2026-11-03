using Microsoft.CodeAnalysis;
using Xunit;

namespace Reef.Generator.Tests;

public class BorderDashTests
{
    private const string COMPANION = """
        namespace ReefMarkupTest
        {
            public partial class V
            {
            }
        }
        """;

    private static MarkupCompilation Compile(string markup, string? rss = null)
    {
        return MarkupTestHost.Compile(markup, COMPANION, rss);
    }

    [Fact]
    public void DashKeywordInShorthandEmitsLineStyle()
    {
        MarkupCompilation result = Compile("""
            <Reef xmlns="Reef" Class="V" Namespace="ReefMarkupTest">
                <Element class="rule" width="10" height="1" />
            </Reef>
            """, ".rule { border-bottom: 1 dashed #ff0000; }");

        Assert.Empty(result.Errors);
        Assert.Contains("LineStyle = global::Reef.UI.BorderLineStyle.Dashed", result.GeneratedText);
        Assert.Contains("BottomThickness = 1", result.GeneratedText);
    }

    [Fact]
    public void DottedKeywordAloneEmitsLineStyleAndThickness()
    {
        MarkupCompilation result = Compile("""
            <Reef xmlns="Reef" Class="V" Namespace="ReefMarkupTest">
                <Element class="rule" width="10" height="1" />
            </Reef>
            """, ".rule { border-style: dotted; border-width: 1; border-color: #ff0000; }");

        Assert.Empty(result.Errors);
        Assert.Contains("LineStyle = global::Reef.UI.BorderLineStyle.Dotted", result.GeneratedText);
    }

    [Fact]
    public void ExplicitDashPatternEmitsDashAndGap()
    {
        MarkupCompilation result = Compile("""
            <Reef xmlns="Reef" Class="V" Namespace="ReefMarkupTest">
                <Element class="rule" width="10" height="1" />
            </Reef>
            """, ".rule { border-bottom: 1 #ff0000; border-dash: 2 4; }");

        Assert.Empty(result.Errors);
        Assert.Contains("Dash = 2", result.GeneratedText);
        Assert.Contains("Gap = 4", result.GeneratedText);
    }

    [Fact]
    public void SingleDashLengthReusesItAsTheGap()
    {
        MarkupCompilation result = Compile("""
            <Reef xmlns="Reef" Class="V" Namespace="ReefMarkupTest">
                <Element width="10" height="1" border-dash="3" border-color="#ff0000" />
            </Reef>
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("Dash = 3", result.GeneratedText);
        Assert.Contains("Gap = 3", result.GeneratedText);
    }

    [Fact]
    public void UnknownLineStyleReportsInvalidEnumValue()
    {
        MarkupCompilation result = Compile("""
            <Reef xmlns="Reef" Class="V" Namespace="ReefMarkupTest">
                <Element class="rule" width="10" height="1" />
            </Reef>
            """, ".rule { border-style: wavy; }");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "RF0023");
    }

    [Fact]
    public void ZeroDashLengthReportsInvalidShorthand()
    {
        MarkupCompilation result = Compile("""
            <Reef xmlns="Reef" Class="V" Namespace="ReefMarkupTest">
                <Element class="rule" width="10" height="1" />
            </Reef>
            """, ".rule { border-dash: 0 4; }");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "RF0018");
    }
}
