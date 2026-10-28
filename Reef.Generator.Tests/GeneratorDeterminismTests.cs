using Xunit;

namespace Reef.Generator.Tests;

public class GeneratorDeterminismTests
{
    private const string MARKUP = """
        <Reef xmlns="Reef" Class="DeterministicView" Namespace="ReefMarkupTest">
            <Element layout="vertical" width="200" background="#102030">
                <Text text="Hello" />
            </Element>
        </Reef>
        """;

    private const string COMPANION = """
        namespace ReefMarkupTest
        {
            public partial class DeterministicView
            {
            }

            public sealed class Extra
            {
            }
        }
        """;

    [Fact]
    public void OutputIsDeterministic()
    {
        MarkupCompilation first = MarkupTestHost.Compile(MARKUP, COMPANION);
        MarkupCompilation second = MarkupTestHost.Compile(MARKUP, COMPANION);

        Assert.Empty(first.Errors);
        Assert.Equal(first.GeneratedText, second.GeneratedText);
    }

    [Fact]
    public void UnrelatedCodeDoesNotChangeOutput()
    {
        MarkupCompilation first = MarkupTestHost.Compile(MARKUP, COMPANION);
        MarkupCompilation second = MarkupTestHost.Compile(MARKUP, COMPANION + "\n// unrelated comment\n");

        Assert.Equal(first.GeneratedText, second.GeneratedText);
    }
}
