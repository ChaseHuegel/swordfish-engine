using Xunit;

namespace Reef.Generator.Tests;

public class WidgetGeneratorTests
{
    [Fact]
    public void ExternalWidgetReceivesStyleAndConstraints()
    {
        const string markup = """
            <Reef xmlns="Reef"
                  xmlns:wb="Reef.Generator.Tests.Widgets"
                  Class="WidgetView"
                  Context="WidgetContext"
                  RendererData="Reef.PixelTexture"
                  Namespace="ReefMarkupTest">
                <wb:Panel width="120" height="40" background="#ff0000" />
            </Reef>
            """;

        const string companion = """
            using Reef;
            using Reef.UI;

            namespace ReefMarkupTest
            {
                public sealed class WidgetContext
                {
                }

                public partial class WidgetView
                {
                }
            }

            namespace Reef.Generator.Tests.Widgets
            {
                public static class Widgets
                {
                    public static void Panel(this UIBuilder<PixelTexture> ui, Style style, Reef.UI.Constraints constraints)
                    {
                        using (ui.Element())
                        {
                            ui.Color = style.BackgroundColor;
                            ui.Constraints = constraints;
                        }
                    }
                }
            }
            """;

        MarkupCompilation result = MarkupTestHost.Compile(markup, companion);
        Assert.Empty(result.Errors);
        Assert.Contains("Widgets.Panel", result.GeneratedText);
        Assert.Contains("style:", result.GeneratedText);
        Assert.Contains("constraints:", result.GeneratedText);
        Assert.True(result.GeneratedText.Contains("new global::Reef.UI.Style()"), result.GeneratedText);
        Assert.True(result.GeneratedText.Contains("new global::Reef.UI.Constraints()"), result.GeneratedText);
    }
}
