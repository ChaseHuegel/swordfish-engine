using Microsoft.Extensions.Logging;
using Reef.Constraints;
using Reef.UI;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;
using System.Threading.Tasks;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class MainMenu : TitleMenu<MenuPage>
{
    private readonly ExternalAppService _externalAppService;
    private readonly ILocalization _localization;
    private readonly ModalMenu _modalMenu;

    private readonly Widgets.IconButtonOptions _brandIconOptions;
    private readonly Widgets.ActionButtonOptions _feedbackButton;

    public MainMenu(
        ILogger<Menu<MenuPage>> logger,
        IAssetDatabase<Material> materialDatabase,
        ReefContext reefContext,
        IShortcutService shortcutService,
        SoundEffectService soundEffectService,
        ExternalAppService externalAppService,
        ILocalization localization,
        ModalMenu modalMenu,
        IMenuPage<MenuPage>[] pages
    ) : base(logger, materialDatabase, reefContext, pages)
    {
        _externalAppService = externalAppService;
        _localization = localization;
        _modalMenu = modalMenu;
        _brandIconOptions = new Widgets.IconButtonOptions(
            FontId: UiTheme.FONT_BRANDS,
            FontSize: UiMetrics.LARGE_ICON_FONT,
            SoundEffectService: soundEffectService
        );
        _feedbackButton = Widgets.ActionButtonOptions.Compact(
            ButtonVariant.Secondary,
            soundEffectService
        ) with
        {
            Font = new FontOptions
            {
                ID = UiTheme.FONT_UI,
                Size = UiMetrics.MENU_SMALL_FONT,
                NoWrap = true,
            },
        };

        Shortcut backShortcut = new(
            name: "Go back",
            category: "General",
            ShortcutModifiers.None,
            Key.Esc,
            isEnabled: IsVisible,
            action: () => GoBack()
        );
        shortcutService.RegisterShortcut(backShortcut);

        WaywardBeyond.GameState.Changed += OnGameStateChanged;
    }

    public override bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.MainMenu;
    }

    public override Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (GetCurrentPage() != MenuPage.Home)
        {
            return base.RenderUI(delta, ui);
        }

        using (ui.Element())
        {
            ui.Padding = new Padding(20);
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Bottom | Anchors.Left,
            };

            if (ui.ActionButton("Button_Feedback", _localization.GetRequired("ui.menu.feedback"), _feedbackButton))
            {
                _modalMenu.GoToPage(FeedbackModal.Modal);
            }
        }

        using (ui.Row(
            new Constraints
            {
                Anchors = Anchors.Bottom | Anchors.Right,
                X = new Fixed(-20),
                Y = new Fixed(-20),
                Width = new Fixed(UiMetrics.ICON_BUTTON_SIZE * 2 + 8),
                Height = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
            },
            spacing: 8
        ))
        {
            var iconConstraints = new Constraints
            {
                Width = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
                Height = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
            };

            if (ui.IconButton("Button_Discord", MenuGlyphs.DISCORD, _brandIconOptions, iconConstraints))
            {
                Task.Run(_externalAppService.TryOpenDiscordAsync);
            }

            if (ui.IconButton("Button_Steam", MenuGlyphs.STEAM, _brandIconOptions, iconConstraints))
            {
                Task.Run(_externalAppService.TryOpenSteamAsync);
            }
        }

        return base.RenderUI(delta, ui);
    }

    private void OnGameStateChanged(object? sender, DataChangedEventArgs<GameState> e)
    {
        if (e.NewValue != GameState.MainMenu)
        {
            return;
        }

        GoToPage(MenuPage.Home);
    }
}
