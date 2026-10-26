using System;
using System.Linq;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.Statistics;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class HomePage(
    in Entry entry,
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in GameSaveService gameSaveService,
    in GameSaveManager gameSaveManager,
    in CharacterSaveManager characterSaveManager,
    in CharacterAssetService characterAssetService,
    in ISkillDatabase skillDatabase,
    in ModalMenu modalMenu
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Home;

    private const int MAX_PAGE_WIDTH = 900;
    private const int PAGE_HEIGHT = 340;
    private const int BUTTON_GAP = 8;
    private const int DIVIDER_HEIGHT = 1;
    private const int DIVIDER_GAP = 8;

    private readonly Entry _entry = entry;
    private readonly ILocalization _localization = localization;
    private readonly GameSaveService _gameSaveService = gameSaveService;
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;
    private readonly CharacterAssetService _characterAssetService = characterAssetService;
    private readonly ISkillDatabase _skillDatabase = skillDatabase;
    private readonly ModalMenu _modalMenu = modalMenu;

    private readonly Widgets.ActionButtonOptions _continueButton = MenuButton(ButtonVariant.Primary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _newGameButton = MenuButton(ButtonVariant.Secondary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _multiplayerButton = MenuButton(ButtonVariant.Secondary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _optionsButton = MenuButton(ButtonVariant.Secondary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _creditsButton = MenuButton(ButtonVariant.Secondary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _exitButton = MenuButton(ButtonVariant.Secondary, soundEffectService);

    private bool _hasShownNotice;

    private static Widgets.ActionButtonOptions MenuButton(ButtonVariant variant, SoundEffectService soundEffectService)
    {
        return Widgets.ActionButtonOptions.Menu(variant, soundEffectService);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        if (!_hasShownNotice)
        {
            _hasShownNotice = true;
            _modalMenu.GoToPage(PlaytestNoticeModal.Modal);
        }

        GameSave[] saves = _gameSaveService.GetSaves();
        GameSave? activeSave = _gameSaveManager.ActiveSave ?? _gameSaveManager.GetMostRecentSave();
        Character? activeCharacter = _characterSaveManager.ActiveSave ?? _characterSaveManager.GetMostRecentSave();

        int pageWidth = Math.Clamp(ui.Width - 80, 360, MAX_PAGE_WIDTH);

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(pageWidth),
                Height = new Fixed(PAGE_HEIGHT),
            };
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(pageWidth),
                Height = new Fixed(PAGE_HEIGHT),
            };

            int buttonX = (pageWidth - UiMetrics.MenuButtonMinWidth) / 2;
            var y = 0;

            if (saves.Length > 0)
            {
                ui.MenuChevron(buttonX - 8, y + (UiMetrics.MenuButtonHeight - 6) / 2);
            }

            if (ui.ActionButton("Button_Continue", _localization.GetString("ui.button.continue")!, _continueButton with { Rect = Widgets.Rect(buttonX, y, UiMetrics.MenuButtonMinWidth, UiMetrics.MenuButtonHeight), Enabled = saves.Length > 0 }, out bool continueHovered))
            {
                menu.GoToPage(MenuPage.SelectSave);
            }

            y += UiMetrics.MenuButtonHeight + BUTTON_GAP;
            if (ui.ActionButton("Button_Singleplayer", _localization.GetString("ui.button.singleplayer")!, _newGameButton with { Rect = Widgets.Rect(buttonX, y, UiMetrics.MenuButtonMinWidth, UiMetrics.MenuButtonHeight), Enabled = _gameSaveService.CanCreateSave }))
            {
                menu.GoToPage(MenuPage.SelectSave);
            }

            y += UiMetrics.MenuButtonHeight + BUTTON_GAP;
            if (ui.ActionButton("Button_Multiplayer", _localization.GetString("ui.button.multiplayer")!, _multiplayerButton with { Rect = Widgets.Rect(buttonX, y, UiMetrics.MenuButtonMinWidth, UiMetrics.MenuButtonHeight) }))
            {
                menu.GoToPage(MenuPage.Multiplayer);
            }

            y += UiMetrics.MenuButtonHeight + DIVIDER_GAP;
            ui.DottedDivider(Widgets.Rect(buttonX, y, UiMetrics.MenuButtonMinWidth, DIVIDER_HEIGHT));
            y += DIVIDER_HEIGHT + DIVIDER_GAP;

            if (ui.ActionButton("Button_Settings", _localization.GetString("ui.button.settings")!, _optionsButton with { Rect = Widgets.Rect(buttonX, y, UiMetrics.MenuButtonMinWidth, UiMetrics.MenuButtonHeight) }))
            {
                menu.GoToPage(MenuPage.Settings);
            }

            y += UiMetrics.MenuButtonHeight + BUTTON_GAP;
            if (ui.ActionButton("Button_Credits", _localization.GetString("ui.menu.credits")!, _creditsButton with { Rect = Widgets.Rect(buttonX, y, UiMetrics.MenuButtonMinWidth, UiMetrics.MenuButtonHeight) }))
            {
                menu.GoToPage(MenuPage.Credits);
            }

            y += UiMetrics.MenuButtonHeight + BUTTON_GAP;
            if (ui.ActionButton("Button_Quit", _localization.GetString("ui.button.quit")!, _exitButton with { Rect = Widgets.Rect(buttonX, y, UiMetrics.MenuButtonMinWidth, UiMetrics.MenuButtonHeight) }))
            {
                _entry.Quit();
            }

            if (continueHovered && saves.Length > 0 && activeSave != null)
            {
                DrawContinueCard(ui, activeSave.Value, activeCharacter, buttonX + UiMetrics.MenuButtonMinWidth + 24, 0, pageWidth - buttonX - UiMetrics.MenuButtonMinWidth - 28);
            }
        }

        return Result.FromSuccess();
    }

    private void DrawContinueCard(
        UIBuilder<Material> ui,
        in GameSave save,
        Character? character,
        int x,
        int y,
        int width
    )
    {
        if (width < 200)
        {
            return;
        }

        const int height = 196;
        using (ui.MenuPanel(Widgets.Rect(x, y, width, height)))
        {
            int padding = 12;
            ui.Label(
                _localization.GetString("ui.menu.activeArchive")!,
                Widgets.Rect(padding, 8, width - padding * 2, 16),
                UiMetrics.MenuSmallFont,
                UiTheme.Primary,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                save.Name,
                Widgets.Rect(padding, 28, width - padding * 2, 20),
                UiMetrics.MenuBodyFont,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetString("ui.menu.mode")!}: {save.Level.DefaultGameMode}",
                Widgets.Rect(padding, 52, width - padding * 2, 16),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetString("ui.menu.seed")!}: {save.Level.Seed}",
                Widgets.Rect(padding, 70, width - padding * 2, 16),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );

            string lastPlayed = save.Level.Playtime.LastPlayedMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(save.Level.Playtime.LastPlayedMs).ToLocalTime().ToString("d")
                : _localization.GetString("ui.label.never")!;
            ui.Label(
                $"{_localization.GetString("ui.label.lastPlayed")!}: {lastPlayed}",
                Widgets.Rect(padding, 88, width - padding * 2, 16),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );

            TimeSpan age = TimeSpan.FromMilliseconds(save.Level.Playtime.AgeMs);
            ui.Label(
                $"{_localization.GetString("ui.label.timePlayed")!}: {_localization.GetLongString(age)}",
                Widgets.Rect(padding, 106, width - padding * 2, 16),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );

            if (character != null)
            {
                Material appearance = _characterAssetService.GetAppearanceMaterial(character.Value);
                using (ui.Image(appearance))
                {
                    ui.Constraints = new Constraints
                    {
                        X = new Fixed(padding),
                        Y = new Fixed(124),
                        Width = new Fixed(64),
                        Height = new Fixed(64),
                    };
                }

                int textX = padding + 76;
                int level = character.Value.GetTotalSkillLevel(_skillDatabase);
                ui.Label(
                    character.Value.Name,
                    Widgets.Rect(textX, 124, width - textX - padding, 18),
                    UiMetrics.MenuSmallFont,
                    UiTheme.AccentBright,
                    Anchors.Left | Anchors.Center
                );
                ui.Label(
                    $"{_localization.GetString("ui.menu.level")!} {level}",
                    Widgets.Rect(textX, 146, width - textX - padding, 18),
                    UiMetrics.MenuSmallFont,
                    UiTheme.Text,
                    Anchors.Left | Anchors.Center
                );
            }
            else
            {
                ui.Label(
                    _localization.GetString("ui.menu.noColonist")!,
                    Widgets.Rect(padding, 132, width - padding * 2, 18),
                    UiMetrics.MenuSmallFont,
                    UiTheme.TextDim,
                    Anchors.Left | Anchors.Center
                );
            }
        }
    }
}
