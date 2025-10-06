using System.Threading.Tasks;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI;

internal sealed class MainMenuHome(in Entry entry) : IMenuPage<MainMenuPage>
{
    public MainMenuPage ID => MainMenuPage.Home;

    private readonly Entry _entry = entry;
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 24,
    };

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MainMenuPage> menu)
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Left,
                X = new Relative(0.01f),
                Y = new Relative(0.5f),
            };

            if (ui.TextButton(id: "Button_NewGame", text: "New game", _buttonFontOptions))
            {
                Task.Run(_entry.StartGameAsync);
            }

            ui.TextButton(id: "Button_ContinueGame", text: "Continue game", _buttonFontOptions);
            ui.TextButton(id: "Button_JoinGame", text: "Join game", _buttonFontOptions);
            ui.TextButton(id: "Button_Settings", text: "Settings", _buttonFontOptions);

            if (ui.TextButton(id: "Button_Quit", text: "Quit", _buttonFontOptions))
            {
                _entry.Quit();
            }
        }
        
        return Result.FromSuccess();
    }
}