using Reef;
using Xunit;

namespace Reef.Generator.Tests;

public class BasicGeneratorTests
{
    [Fact]
    public void SimpleViewCompilesAndRenders()
    {
        const string markup = """
            <Reef xmlns="Reef"
                  Class="TestView"
                  Context="TestContext"
                  Namespace="ReefMarkupTest">
                <Element layout="vertical" width="200" height="100" background="#102030">
                    <Text text="Hello" size="18" />
                </Element>
            </Reef>
            """;

        const string companion = """
            namespace ReefMarkupTest;

            public sealed class TestContext
            {
                public string Name { get; set; } = "Player";
            }

            public partial class TestView
            {
            }
            """;

        MarkupCompilation result = MarkupTestHost.Compile(markup, companion);
        Assert.Empty(result.Errors);
        Assert.Contains("partial class TestView", result.GeneratedText);
        Assert.Contains("ui.Text(\"Hello\")", result.GeneratedText);

        var assembly = MarkupTestHost.EmitAssembly(result);
        object context = MarkupTestHost.CreateContext(assembly, "ReefMarkupTest.TestContext");
        List<RenderCommand<PixelTexture>> commands = MarkupTestHost.Render(assembly, "ReefMarkupTest.TestView", context);
        Assert.True(
            commands.Any(command => command.Text == "Hello"),
            result.GeneratedText.Substring(0, Math.Min(4000, result.GeneratedText.Length)) + "\nCommands: " + commands.Count);
    }
}
