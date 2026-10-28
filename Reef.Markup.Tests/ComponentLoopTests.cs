using Reef;
using Reef.UI;

namespace Reef.Markup.Tests;

public class ComponentLoopTests
{
    [Fact]
    public void ComponentInstancesInLoopGetUniqueIds()
    {
        var ui = new UIBuilder<PixelTexture>(400, 300, new StubTextEngine(), new UIController());
        var view = new PlayerListView();
        var context = new PlayerListContext();

        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);

        Assert.Contains(commands, command => command.Text == "Ada");
        Assert.Contains(commands, command => command.Text == "Bob");
        List<string> ids = commands
            .Where(command => command.ID != null && command.ID.Contains("__rml_", StringComparison.Ordinal))
            .Select(command => command.ID!)
            .ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.True(ids.Count >= 2);
    }
}
