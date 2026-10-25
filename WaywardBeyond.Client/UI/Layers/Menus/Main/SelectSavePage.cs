using System;
using System.Collections.Generic;
using System.Linq;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class SelectSavePage : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectSave;

    private const int CARD_HEIGHT = 40;
    private const int CARD_GAP = 8;
    private const int PANEL_PADDING = 12;
    private const int REFRESH_INTERVAL_MS = 1000;

    private readonly GameSaveManager _gameSaveManager;
    private readonly GameSaveService _gameSaveService;
    private readonly CharacterSaveManager _characterSaveManager;
    private readonly CharacterAssetService _characterAssetService;
    private readonly IInputService _inputService;
    private readonly ILocalization _localization;
    private readonly ICharacterStorage _characterStorage;
    private readonly ModalMenu _modalMenu;
    private readonly ConfirmModal _confirmModal;
    private readonly SoundEffectService _soundEffectService;
    private readonly ClientRpcHost _rpcHost;
    private readonly NetworkingConfig _networkingConfig;
    private readonly ProfileSettings _profileSettings;

    private readonly Widgets.ActionButtonOptions _returnButton;

    private bool _remoteConnectTried;
    private long _lastRenderMs;
    private long _lastRefreshMs;
    private int _scrollY;
    private int _narrowTab;

    public SelectSavePage(
        in GameSaveManager gameSaveManager,
        in GameSaveService gameSaveService,
        in CharacterSaveManager characterSaveManager,
        in CharacterAssetService characterAssetService,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in ICharacterStorage characterStorage,
        in ModalMenu modalMenu,
        in ConfirmModal confirmModal,
        in ClientRpcHost rpcHost,
        in NetworkingConfig networkingConfig,
        in ProfileSettings profileSettings
    ) {
        _gameSaveManager = gameSaveManager;
        _gameSaveService = gameSaveService;
        _characterSaveManager = characterSaveManager;
        _characterAssetService = characterAssetService;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _characterStorage = characterStorage;
        _modalMenu = modalMenu;
        _confirmModal = confirmModal;
        _rpcHost = rpcHost;
        _networkingConfig = networkingConfig;
        _profileSettings = profileSettings;

        _returnButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        EnsureRemoteConnection();
        RefreshSaveListIfDue();
        _gameSaveManager.ActiveSave ??= _gameSaveManager.GetMostRecentSave();

        MenuLayout layout = MenuLayout.Compute(
            ui.Width,
            ui.Height,
            maxPageWidth: 1160,
            twoColumn: true,
            leftRatio: 0.34f
        );

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };

            if (layout.UseTabs)
            {
                DrawTabs(ui, layout);
                if (_narrowTab == 0)
                {
                    DrawArchives(ui, layout.Right, menu);
                }
                else
                {
                    DrawTelemetry(ui, layout.Right, menu);
                }
            }
            else
            {
                DrawArchives(ui, layout.Left, menu);
                DrawTelemetry(ui, layout.Right, menu);
            }

            if (ui.ActionButton("Button_Back", _localization.GetString("ui.button.return")!, _returnButton with { Rect = layout.ReturnButton }))
            {
                menu.GoToPage(MenuPage.Home);
            }
        }

        return Result.FromSuccess();
    }

    private void DrawTabs(UIBuilder<Material> ui, in MenuLayout layout)
    {
        int tabWidth = layout.PageWidth / 2;
        if (ui.Tab(
                "ArchivesTab",
                Widgets.Rect(0, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y),
                _localization.GetString("ui.menu.galacticArchives")!,
                selected: _narrowTab == 0
            ))
        {
            _narrowTab = 0;
        }

        if (ui.Tab(
                "TelemetryTab",
                Widgets.Rect(tabWidth + 4, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y),
                _localization.GetString("ui.menu.sectorTelemetry")!,
                selected: _narrowTab == 1
            ))
        {
            _narrowTab = 1;
        }
    }

    private void DrawArchives(UIBuilder<Material> ui, IntRect rect, Menu<MenuPage> menu)
    {
        using (ui.BracketPanel(rect, _localization.GetString("ui.menu.galacticArchives")!))
        {
            GameSave[] saves = _gameSaveService.GetSaves();
            int contentWidth = Math.Max(1, rect.Size.X - PANEL_PADDING * 2);
            int newButtonHeight = 32;
            int listY = 32;
            int listHeight = Math.Max(0, rect.Size.Y - listY - newButtonHeight - PANEL_PADDING - 8);

            IntRect listRect = Widgets.Rect(PANEL_PADDING, listY, contentWidth, listHeight);
            using (ui.Element())
            {
                ui.Color = UiTheme.Transparent;
                ui.VerticalScroll = true;
                ui.ScrollY = -_scrollY;
                ui.Constraints = Widgets.RectConstraints(listRect);
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(listRect.Size.X),
                    Height = new Fixed(listRect.Size.Y),
                };

                int contentHeight = saves.Length * (CARD_HEIGHT + CARD_GAP);
                int maxScroll = Math.Max(0, contentHeight - listHeight);
                float wheel = _inputService.GetMouseScroll();
                if (wheel != 0f)
                {
                    _scrollY -= (int)MathF.Round(wheel * UiMetrics.ScrollStep);
                }
                _scrollY = Math.Clamp(_scrollY, 0, maxScroll);
                ui.ScrollY = -_scrollY;

                for (var i = 0; i < saves.Length; i++)
                {
                    GameSave save = saves[i];
                    bool selected = _gameSaveManager.ActiveSave != null &&
                        _gameSaveManager.ActiveSave.Value.Level.Guid == save.Level.Guid;
                    int cardY = i * (CARD_HEIGHT + CARD_GAP);
                    IntRect cardRect = Widgets.Rect(0, cardY, contentWidth, CARD_HEIGHT);

                    bool clicked = ui.SelectableCard(
                        $"Button_SelectSave_{save.Name}",
                        cardRect,
                        new Widgets.SelectableCardOptions(selected, SoundEffectService: _soundEffectService),
                        contentUi =>
                        {
                            contentUi.Label(
                                MenuGlyphs.Galaxy,
                                Widgets.Rect(8, 8, 24, 24),
                                20,
                                selected ? UiTheme.AccentBright : UiTheme.Accent,
                                Anchors.Center,
                                UiTheme.FontIcons
                            );
                            contentUi.Label(
                                save.Name,
                                Widgets.Rect(36, 0, Math.Max(1, contentWidth - 80), CARD_HEIGHT),
                                UiMetrics.MenuBodyFont,
                                selected ? UiTheme.AccentBright : UiTheme.Text,
                                Anchors.Left | Anchors.Center
                            );

                            if (contentUi.ActionButton(
                                    $"Button_DeleteSave_{save.Name}",
                                    Widgets.Rect(contentWidth - 34, 6, 28, 28),
                                    MenuGlyphs.Trash,
                                    ButtonVariant.Danger,
                                    fontId: UiTheme.FontIcons,
                                    soundEffectService: _soundEffectService
                                ))
                            {
                                _gameSaveManager.ActiveSave = save;
                                _modalMenu.GoToPage(_confirmModal.Create(DeleteSave));
                            }

                            void DeleteSave()
                            {
                                _gameSaveManager.Delete(save);
                                _gameSaveManager.ActiveSave = _gameSaveManager.GetMostRecentSave();
                                _scrollY = 0;
                            }
                        }
                    );

                    if (clicked)
                    {
                        _gameSaveManager.ActiveSave = save;
                    }
                }
            }

            IntRect newButtonRect = Widgets.Rect(
                PANEL_PADDING,
                rect.Size.Y - newButtonHeight - PANEL_PADDING,
                contentWidth,
                newButtonHeight
            );
            if (_gameSaveService.CanCreateSave && ui.ActionButton(
                    "Button_NewSave",
                    newButtonRect,
                    "+ " + _localization.GetString("ui.menu.chartNewGalaxy")!,
                    ButtonVariant.Subtle
                ))
            {
                menu.GoToPage(MenuPage.NewSave);
            }

            if (saves.Length == 0 && listHeight > 24)
            {
                ui.Label(
                    _localization.GetString("ui.text.none")!,
                    Widgets.Rect(PANEL_PADDING, listY + 12, contentWidth, 20),
                    UiMetrics.MenuBodyFont,
                    UiTheme.TextDim,
                    Anchors.Center
                );
            }
        }
    }

    private void DrawTelemetry(UIBuilder<Material> ui, IntRect rect, Menu<MenuPage> menu)
    {
        using (ui.BracketPanel(rect, _localization.GetString("ui.menu.sectorTelemetry")!))
        {
            GameSave? activeSave = _gameSaveManager.ActiveSave ?? _gameSaveManager.GetMostRecentSave();
            if (activeSave == null)
            {
                ui.Label(
                    _localization.GetString("ui.text.none")!,
                    Widgets.Rect(0, 44, rect.Size.X, 24),
                    UiMetrics.MenuBodyFont,
                    UiTheme.TextDim,
                    Anchors.Center
                );
                return;
            }

            GameSave save = activeSave.Value;
            Character? character = _characterSaveManager.ActiveSave ?? _characterSaveManager.GetMostRecentSave();
            int padding = 16;
            int textX = padding;

            ui.Label(
                save.Name,
                Widgets.Rect(padding, 34, rect.Size.X - padding * 2, 24),
                UiMetrics.MenuTitleFont,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetString("ui.menu.mode")!}: {save.Level.DefaultGameMode}",
                Widgets.Rect(padding, 64, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetString("ui.menu.seed")!}: {save.Level.Seed}",
                Widgets.Rect(padding, 82, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );

            string lastPlayed = save.Level.Playtime.LastPlayedMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(save.Level.Playtime.LastPlayedMs).ToLocalTime().ToString("g")
                : _localization.GetString("ui.label.never")!;
            ui.Label(
                $"{_localization.GetString("ui.label.lastPlayed")!}: {lastPlayed}",
                Widgets.Rect(padding, 100, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );

            TimeSpan age = TimeSpan.FromMilliseconds(save.Level.Playtime.AgeMs);
            ui.Label(
                $"{_localization.GetString("ui.label.timePlayed")!}: {_localization.GetLongString(age)}",
                Widgets.Rect(padding, 118, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );

            int previewY = 150;
            if (character != null)
            {
                Material appearance = _characterAssetService.GetAppearanceMaterial(character.Value);
                using (ui.Image(appearance))
                {
                    ui.Constraints = new Constraints
                    {
                        X = new Fixed(padding),
                        Y = new Fixed(previewY),
                        Width = new Fixed(72),
                        Height = new Fixed(72),
                    };
                }

                int statsX = padding + 84;
                ui.Label(
                    character.Value.Name,
                    Widgets.Rect(statsX, previewY, rect.Size.X - statsX - padding, 22),
                    UiMetrics.MenuBodyFont,
                    UiTheme.AccentBright,
                    Anchors.Left | Anchors.Center
                );
                ui.Label(
                    $"S:{character.Value.Strength} P:{character.Value.Precision} A:{character.Value.Awareness}",
                    Widgets.Rect(statsX, previewY + 24, rect.Size.X - statsX - padding, 18),
                    UiMetrics.MenuSmallFont,
                    UiTheme.Text,
                    Anchors.Left | Anchors.Center
                );
                ui.Label(
                    $"C:{character.Value.Charisma} E:{character.Value.Education} R:{character.Value.Resolve}",
                    Widgets.Rect(statsX, previewY + 42, rect.Size.X - statsX - padding, 18),
                    UiMetrics.MenuSmallFont,
                    UiTheme.Text,
                    Anchors.Left | Anchors.Center
                );
            }
            else
            {
                ui.Label(
                    _localization.GetString("ui.menu.noColonist")!,
                    Widgets.Rect(padding, previewY + 16, rect.Size.X - padding * 2, 20),
                    UiMetrics.MenuSmallFont,
                    UiTheme.TextDim,
                    Anchors.Left | Anchors.Center
                );
            }

            int buttonY = rect.Size.Y - 42;
            if (ui.ActionButton(
                    "Button_Embark",
                    Widgets.Rect(rect.Size.X - 156, buttonY, 140, 30),
                    _localization.GetString("ui.button.loadSave")!,
                    ButtonVariant.Primary,
                    soundEffectService: _soundEffectService
                ))
            {
                IEnumerable<Character> characters = _characterStorage.GetAllCharacters();
                menu.GoToPage(characters.Any() ? MenuPage.SelectCharacter : MenuPage.NewCharacter);
            }
        }
    }

    private void RefreshSaveListIfDue()
    {
        long now = Environment.TickCount64;

        bool entered = now - _lastRenderMs > 250;
        _lastRenderMs = now;

        if (!entered && now - _lastRefreshMs < REFRESH_INTERVAL_MS)
        {
            return;
        }

        _lastRefreshMs = now;
        _ = _gameSaveService.RefreshLevelsAsync();
    }

    private void EnsureRemoteConnection()
    {
        if (_profileSettings.LastServerMode.Get() != LastServerMode.Remote
            || _rpcHost.IsRemote
            || _remoteConnectTried)
        {
            return;
        }

        _remoteConnectTried = true;
        Result result = _rpcHost.ConnectRemote(_networkingConfig.RemoteHost.Get(), _networkingConfig.RemotePort.Get());
        if (result.Success)
        {
            _ = _gameSaveService.RefreshLevelsAsync();
        }
    }
}
