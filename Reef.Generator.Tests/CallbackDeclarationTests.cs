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
}
