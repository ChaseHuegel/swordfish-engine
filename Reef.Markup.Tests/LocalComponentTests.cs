using Reef;
using Reef.Markup.TestComponents;
using Reef.UI;

namespace Reef.Markup.Tests;

public class LocalComponentTests
{
    [Fact]
    public void LocalComponentInlinesAndMergesCallerStyle()
    {
        var ui = new UIBuilder<PixelTexture>(400, 300, new StubTextEngine(), new UIController());
        var view = new LocalView();
        var context = new PlayerInfo { Name = "Ada", Score = 42 };

        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);

        Assert.Contains(commands, command => command.Text == "Ada");
        Assert.Contains(commands, command => command.Text == "Local 42");

        RenderCommand<PixelTexture> card = commands.First(command =>
            command.Text == null && command.Rect.Size.X == 240 && command.Color.X > 0.17f);
        Assert.Equal(0.1882353f, card.Color.X, 3);
    }
}
