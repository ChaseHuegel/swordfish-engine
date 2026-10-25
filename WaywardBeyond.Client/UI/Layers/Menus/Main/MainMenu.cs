using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class MainMenu : TitleMenu<MenuPage>
{
    private readonly ExternalAppService _externalAppService;
    private readonly ILocalization _localization;
    private readonly ModalMenu _modalMenu;

    private readonly Widgets.ActionButtonOptions _brandIconButton;
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
        _brandIconButton = Widgets.ActionButtonOptions.Icon(
            ButtonVariant.Secondary,
            soundEffectService,
            size: UiMetrics.IconButtonSize,
            fontSize: UiMetrics.LargeIconFont,
            fontId: UiTheme.FontBrands
        );
        _feedbackButton = Widgets.ActionButtonOptions.Compact(
            ButtonVariant.Secondary,
            soundEffectService
        ) with
        {
            Font = new FontOptions
            {
                ID = UiTheme.FontUI,
                Size = UiMetrics.MenuSmallFont,
                NoWrap = true,
            },
        };

        //  TODO introduce the menu spaceship
        // Result<VoxelEntityModel> blueprintResult = blueprintDatabase.Get("builtin/mainMenu");
        // if (blueprintResult)
        // {
        //     VoxelEntityModel voxelEntityModel = blueprintResult.Value;
        //     VoxelEntityBuilder.Create(voxelEntityModel.Uuid, voxelEntityModel.VoxelObject, new Vector3(5f, 0f, -20f), Quaternion.Identity, Vector3.One);
        // }
        // else
        // {
        //     logger.LogError(blueprintResult, "Failed to load the background blueprint, it will not be displayed.");
        // }

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
        if (GetCurrentPage() == MenuPage.Home)
        {
            using (ui.Element())
            {
                ui.Padding = new Padding(20);
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Bottom | Anchors.Left,
                };

                if (ui.ActionButton("Button_Feedback", _localization.GetString("ui.menu.feedback")!, _feedbackButton))
                {
                    _modalMenu.GoToPage(FeedbackModal.Modal);
                }
            }

            using (ui.Element())
            {
                ui.Padding = new Padding(20);
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Bottom | Anchors.Right,
                };

                if (ui.ActionButton("Button_Discord", MenuGlyphs.Discord, _brandIconButton))
                {
                    Task.Run(_externalAppService.TryOpenDiscordAsync);
                }

                if (ui.ActionButton("Button_Steam", MenuGlyphs.Steam, _brandIconButton))
                {
                    Task.Run(_externalAppService.TryOpenSteamAsync);
                }
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
