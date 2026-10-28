using Reef;
using Reef.UI;

namespace Reef.Markup.Tests;

public class InstanceRuntimeTests
{
    [Fact]
    public void CrossAssemblyComponentInstanceIsPassed()
    {
        var ui = new UIBuilder<PixelTexture>(300, 200, new StubTextEngine(), new UIController());
        var view = new InstanceView();
        var context = new InstanceContext();

        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);

        Assert.Contains(commands, command => command.Text == "Hello World");

        List<string> componentIds = commands
            .Where(command => command.ID != null && command.ID.StartsWith("__rml_", StringComparison.Ordinal))
            .Select(command => command.ID!)
            .Distinct()
            .ToList();
        Assert.Equal(2, componentIds.Count);
    }
}
