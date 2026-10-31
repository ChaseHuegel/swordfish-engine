using Xunit;

namespace Reef.Generator.Tests;

public class CallbackDeclarationTests
{
    [Fact]
    public void EmitsDefinitionForPartialImplementation()
    {
        const string markup = """
            <Reef xmlns="Reef" Class="V" Namespace="ReefMarkupTest">
                <Element onclick="OnClick">
                    <Text text="Hi" />
                </Element>
            </Reef>
            """;
        const string companion = """
            namespace ReefMarkupTest
            {
                public partial class V
                {
                    partial void OnClick()
                    {
                    }
                }
            }
            """;
        MarkupCompilation result = MarkupTestHost.Compile(markup, companion);
        Assert.True(result.Errors.Count == 0, result.GeneratedText + "\n" + string.Join("\n", result.Errors));
        Assert.Contains("partial void OnClick();", result.GeneratedText);
    }

    [Fact]
    public void EmitsEnterExitAndFocus()
    {
        const string markup = """
            <Reef xmlns="Reef" Class="V" Namespace="ReefMarkupTest">
                <Element id="node" onenter="OnEnter" onexit="OnExit" focus="true" />
            </Reef>
            """;
        const string companion = """
            namespace ReefMarkupTest
            {
                public partial class V
                {
                    partial void OnEnter()
                    {
                    }

                    partial void OnExit()
                    {
                    }
                }
            }
            """;
        MarkupCompilation result = MarkupTestHost.Compile(markup, companion);
        Assert.True(result.Errors.Count == 0, result.GeneratedText + "\n" + string.Join("\n", result.Errors));
        Assert.Contains("Entered()", result.GeneratedText);
        Assert.Contains("Exited()", result.GeneratedText);
        Assert.Contains("Focus()", result.GeneratedText);
    }
}
