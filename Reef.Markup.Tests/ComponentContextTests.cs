using Reef;
using Reef.UI;

namespace Reef.Markup.Tests;

public class ComponentContextTests
{
    [Fact]
    public void ComplexContextExpressionIsPreserved()
    {
        var ui = new UIBuilder<PixelTexture>(300, 200, new StubTextEngine(), new UIController());
        var view = new PlayerIndexView();
        var context = new PlayerListContext();

        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);

        Assert.Contains(commands, command => command.Text == "Ada");
        Assert.DoesNotContain(commands, command => command.Text == "Bob");
    }
}
