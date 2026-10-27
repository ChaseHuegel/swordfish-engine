using Xunit;

namespace Reef.Generator.Tests;

public class HoverExpressionTests
{
    [Fact]
    public void VisibilityCanBindToHoverOfAnotherElement()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="V" Context="Ctx">
                <Element id="Button_Continue" width="100" height="40" />
                <Element visible="@ui.Hovering('Button_Continue')" width="100" height="40" />
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public sealed class Ctx
                {
                }

                public partial class V
                {
                }
            }
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("Hovering(\"Button_Continue\")", result.GeneratedText);
    }
}
