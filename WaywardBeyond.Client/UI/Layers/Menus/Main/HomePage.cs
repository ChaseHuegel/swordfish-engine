using System;
using System.Collections.Generic;
using System.Linq;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Networking;
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
    in ICharacterStorage characterStorage,
    in CharacterAssetService characterAssetService,
    in ISkillDatabase skillDatabase,
    in ClientRpcHost rpcHost,
    in ProfileSettings profileSettings,
    in ContinueService continueService,
    in MessageModal messageModal,
    in ModalMenu modalMenu
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Home;

    private const int MAX_PAGE_WIDTH = 900;
    private const int PAGE_HEIGHT = 340;
    private const int BUTTON_GAP = 8;
    private const int DIVIDER_HEIGHT = 1;
    private const int DIVIDER_GAP = 8;
    private const int CONTINUE_CARD_GAP = 24;
    private const int CONTINUE_CARD_MARGIN = 28;
    private const int CONTINUE_CARD_MIN_WIDTH = 200;
    private const int CONTINUE_CARD_METADATA_TOP = 52;
    private const int CONTINUE_CARD_METADATA_TOP_REMOTE = 74;
    private const int CONTINUE_CARD_METADATA_HEIGHT = 4 * 18 + 8;
    private const int CONTINUE_CARD_DIVIDER_GAP = 12;
    private const int CONTINUE_CARD_CHARACTER_HEIGHT = 64 + 8;

    private readonly Entry _entry = entry;
    private readonly ILocalization _localization = localization;
    private readonly GameSaveService _gameSaveService = gameSaveService;
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly ICharacterStorage _characterStorage = characterStorage;
    private readonly CharacterAssetService _characterAssetService = characterAssetService;
    private readonly ISkillDatabase _skillDatabase = skillDatabase;
    private readonly ClientRpcHost _rpcHost = rpcHost;
    private readonly ProfileSettings _profileSettings = profileSettings;
    private readonly ContinueService _continueService = continueService;
    private readonly MessageModal _messageModal = messageModal;
    private readonly ModalMenu _modalMenu = modalMenu;

    private readonly Widgets.ActionButtonOptions _continueButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _newGameButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _multiplayerButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _optionsButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _creditsButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _exitButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);

    private bool _hasShownNotice;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        if (!_hasShownNotice)
        {
            _hasShownNotice = true;
            _modalMenu.GoToPage(PlaytestNoticeModal.Modal);
        }

        if (_continueService.TryDequeueFailure(out ContinueService.Failure failure))
        {
            _modalMenu.GoToPage(_messageModal.Create(failure.Title, failure.Body));
        }

        ContinueState continueState = BuildContinueState();
        int pageWidth = Math.Clamp(ui.Width - 80, 360, MAX_PAGE_WIDTH);
        int buttonOffset = (pageWidth - UiMetrics.MENU_BUTTON_MIN_WIDTH) / 2;
        MenuColumnResult buttons;

        using (ui.Row(
            new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(pageWidth),
            }
        ))
        {
            ui.Spacer(width: buttonOffset);

            buttons = DrawMenuButtons(ui, menu, continueState);

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                };

                if (buttons.ContinueHovered && continueState.HasLastSession && continueState.LastCharacter != null)
                {
                    int cardHeight = ContinueCardHeight(continueState.Remote);
                    int cardWidth = (pageWidth - UiMetrics.MENU_BUTTON_MIN_WIDTH) / 2 - CONTINUE_CARD_GAP - CONTINUE_CARD_MARGIN;
                    if (cardWidth >= CONTINUE_CARD_MIN_WIDTH)
                    {
                        using (ui.Element())
                        {
                            ui.LayoutDirection = LayoutDirection.None;
                            ui.Padding = new Padding(
                                left: CONTINUE_CARD_GAP,
                                top: 0,
                                right: CONTINUE_CARD_MARGIN,
                                bottom: 0
                            );
                            ui.Constraints = new Constraints
                            {
                                Width = new Fill(),
                                Height = new Fixed(cardHeight),
                            };

                            DrawContinueCard(
                                ui,
                                continueState.Details,
                                continueState.LastCharacter.Value,
                                continueState.Remote,
                                continueState.Destination,
                                new Constraints
                                {
                                    Width = new Fill(),
                                    Height = new Fixed(cardHeight),
                                }
                            );
                        }
                    }
                }
            }
        }

        if (buttons.SingleplayerClicked)
        {
            StartSingleplayer(menu);
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the Continue chevron and the home menu buttons, and reports what the page must react to.</summary>
    private MenuColumnResult DrawMenuButtons(
        UIBuilder<Material> ui,
        Menu<MenuPage> menu,
        in ContinueState continueState
    )
    {
        using (ui.Column(
            new Constraints
            {
                Width = new Fixed(UiMetrics.MENU_BUTTON_MIN_WIDTH),
            }
        ))
        {
            var continueOptions = _continueButton with
            {
                Enabled = continueState.HasLastSession,
                Layout = ButtonSlot(),
            };
            bool continueClicked = ui.ActionButton(
                "Button_Continue",
                _localization.GetRequired("ui.button.continue"),
                continueOptions,
                out bool continueHovered
            );
            if (continueClicked)
            {
                _continueService.Start();
            }

            ui.Spacer(height: BUTTON_GAP);

            bool singleplayerClicked = ui.ActionButton(
                "Button_Singleplayer",
                _localization.GetRequired("ui.button.singleplayer"),
                _newGameButton with { Layout = ButtonSlot() }
            );

            ui.Spacer(height: BUTTON_GAP);

            if (ui.ActionButton(
                    "Button_Multiplayer",
                    _localization.GetRequired("ui.button.multiplayer"),
                    _multiplayerButton with { Layout = ButtonSlot() }
                ))
            {
                menu.GoToPage(MenuPage.Multiplayer);
            }

            ui.Spacer(height: DIVIDER_GAP);
            ui.DottedDivider(Widgets.Rect(0, 0, UiMetrics.MENU_BUTTON_MIN_WIDTH, DIVIDER_HEIGHT));
            ui.Spacer(height: DIVIDER_GAP);

            if (ui.ActionButton(
                    "Button_Settings",
                    _localization.GetRequired("ui.button.settings"),
                    _optionsButton with { Layout = ButtonSlot() }
                ))
            {
                menu.GoToPage(MenuPage.Settings);
            }

            ui.Spacer(height: BUTTON_GAP);

            if (ui.ActionButton(
                    "Button_Credits",
                    _localization.GetRequired("ui.menu.credits"),
                    _creditsButton with { Layout = ButtonSlot() }
                ))
            {
                menu.GoToPage(MenuPage.Credits);
            }

            ui.Spacer(height: BUTTON_GAP);

            if (ui.ActionButton(
                    "Button_Quit",
                    _localization.GetRequired("ui.button.quit"),
                    _exitButton with { Layout = ButtonSlot() }
                ))
            {
                _entry.Quit();
            }

            return new MenuColumnResult(continueHovered, singleplayerClicked);
        }
    }

    private static Constraints ButtonSlot()
    {
        return new Constraints
        {
            Width = new Fill(),
            Height = new Fixed(UiMetrics.MENU_BUTTON_HEIGHT),
        };
    }

    private static int ContinueCardHeight(bool remote)
    {
        int metadataY = remote ? CONTINUE_CARD_METADATA_TOP_REMOTE : CONTINUE_CARD_METADATA_TOP;
        return metadataY
            + CONTINUE_CARD_METADATA_HEIGHT
            + CONTINUE_CARD_DIVIDER_GAP
            + CONTINUE_CARD_CHARACTER_HEIGHT;
    }

    /// <summary>Builds the Continue button state from the save listing and the recorded profile summary.</summary>
    private ContinueState BuildContinueState()
    {
        GameSave? lastSave = FindLastSave();
        Character? lastCharacter = FindLastCharacter();

        //  Prefer the live listing, and fall back to the recorded summary when it does not hold the last save.
        ContinueDetails details = lastSave != null
            ? DescribeSave(lastSave.Value)
            : DescribeProfile();

        bool hasLastSession = _continueService.HasLastSession
            && lastCharacter != null
            && !string.IsNullOrWhiteSpace(details.Name);

        bool remote = _profileSettings.LastSession.Server.Get().Mode == ServerMode.Remote;
        string? destination = remote ? ResolveRemoteDestination() : null;

        return new ContinueState(details, hasLastSession, lastCharacter, remote, destination);
    }

    /// <summary>Finds the save the profile last opened.</summary>
    private GameSave? FindLastSave()
    {
        string lastSaveGuid = _profileSettings.LastSession.Save.Get().Guid;
        GameSave[] saves = _gameSaveService.GetSaves();
        for (var i = 0; i < saves.Length; i++)
        {
            if (saves[i].Level.Guid == lastSaveGuid)
            {
                return saves[i];
            }
        }

        return null;
    }

    /// <summary>Finds the character the profile last played.</summary>
    private Character? FindLastCharacter()
    {
        string lastCharacterUuid = _profileSettings.LastSession.CharacterUuid.Get();
        IReadOnlyList<Character> characters = _characterStorage.GetAllCharacters();
        for (var i = 0; i < characters.Count; i++)
        {
            if (characters[i].Uuid.ToString() == lastCharacterUuid)
            {
                return characters[i];
            }
        }

        return null;
    }

    /// <summary>Describes the last save from the live save listing.</summary>
    private static ContinueDetails DescribeSave(in GameSave save)
    {
        return new ContinueDetails(
            save.Name,
            save.Level.DefaultGameMode,
            save.Level.Seed,
            save.Level.Playtime.LastPlayedMs,
            save.Level.Playtime.AgeMs
        );
    }

    /// <summary>Describes the last save from the recorded profile summary.</summary>
    private ContinueDetails DescribeProfile()
    {
        SessionSave save = _profileSettings.LastSession.Save.Get();
        return new ContinueDetails(
            save.Name,
            save.GameMode,
            save.Seed,
            save.LastPlayedMs,
            save.AgeMs
        );
    }

    /// <summary>Gets the label the Continue card shows for the last remote server.</summary>
    private string ResolveRemoteDestination()
    {
        SessionServer server = _profileSettings.LastSession.Server.Get();
        string host = server.Host;
        int port = server.Port;
        string? name = null;
        List<SavedServer> savedServers = _profileSettings.SavedServers.Get();
        for (var i = 0; i < savedServers.Count; i++)
        {
            if (savedServers[i].Host == host && savedServers[i].Port == port)
            {
                name = savedServers[i].Name;
                break;
            }
        }

        return !string.IsNullOrWhiteSpace(name) ? name : $"{host}:{port}";
    }

    private void StartSingleplayer(Menu<MenuPage> menu)
    {
        _rpcHost.Disconnect();
        _gameSaveService.ClearListing();
        _gameSaveManager.ActiveSave = null;
        menu.GoToPage(MenuPage.SelectSave);
    }

    private void DrawContinueCard(
        UIBuilder<Material> ui,
        ContinueDetails details,
        in Character character,
        bool remote,
        string? destination,
        Constraints constraints
    )
    {
        const int labelWidth = 100;
        using (ui.MenuPanel(constraints))
        {
            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(16),
                },
                spacing: 6
            ))
            {
                if (remote)
                {
                    ui.Label(
                        MenuGlyphs.SERVER,
                        UiMetrics.MENU_SMALL_FONT,
                        UiTheme.Accent,
                        new Constraints
                        {
                            Width = new Fixed(16),
                            Height = new Fill(),
                        },
                        fontId: UiTheme.FONT_ICONS
                    );
                }

                ui.Label(
                    remote
                        ? _localization.GetRequired("ui.button.multiplayer")
                        : _localization.GetRequired("ui.button.singleplayer"),
                    UiMetrics.MENU_SMALL_FONT,
                    remote ? UiTheme.Accent : UiTheme.TextMuted,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    }
                );
            }

            ui.Label(
                details.Name,
                UiMetrics.MENU_BODY_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(20),
                }
            );

            if (remote && !string.IsNullOrWhiteSpace(destination))
            {
                ui.Label(
                    destination,
                    UiMetrics.MENU_SMALL_FONT,
                    UiTheme.TextMuted,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fixed(16),
                    }
                );
            }

            ui.Spacer(height: 4);

            string lastPlayed = details.LastPlayedMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(details.LastPlayedMs).ToLocalTime().ToString("d")
                : _localization.GetRequired("ui.label.never");
            TimeSpan age = TimeSpan.FromMilliseconds(details.AgeMs);

            DrawContinueDetail(ui, labelWidth, _localization.GetRequired("ui.menu.mode"), details.Mode.ToString());
            DrawContinueDetail(ui, labelWidth, _localization.GetRequired("ui.menu.seed"), details.Seed.ToString());
            DrawContinueDetail(ui, labelWidth, _localization.GetRequired("ui.label.lastPlayed"), lastPlayed);
            DrawContinueDetail(
                ui,
                labelWidth,
                _localization.GetRequired("ui.label.timePlayed"),
                _localization.GetLongString(age)
            );

            ui.Filler();
            ui.Divider(
                UiTheme.BorderInner,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(1),
                }
            );
            ui.Spacer(height: 8);

            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(64),
                },
                spacing: 12
            ))
            {
                Material appearance = _characterAssetService.GetAppearanceMaterial(character);
                using (ui.Image(appearance))
                {
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(64),
                        Height = new Fixed(64),
                    };
                }

                using (ui.Column(
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    }
                ))
                {
                    int level = character.GetTotalSkillLevel(_skillDatabase);
                    ui.Label(
                        character.Name,
                        UiMetrics.MENU_SMALL_FONT,
                        UiTheme.AccentBright,
                        new Constraints
                        {
                            Width = new Fill(),
                            Height = new Fixed(18),
                        }
                    );
                    ui.Label(
                        $"{_localization.GetRequired("ui.menu.level")} {level}",
                        UiMetrics.MENU_SMALL_FONT,
                        UiTheme.Text,
                        new Constraints
                        {
                            Width = new Fill(),
                            Height = new Fixed(18),
                        }
                    );
                }
            }
        }
    }

    private static void DrawContinueDetail(
        UIBuilder<Material> ui,
        int labelWidth,
        string label,
        string value
    )
    {
        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(18),
            }
        ))
        {
            ui.Label(
                label,
                UiMetrics.MENU_LABEL_FONT,
                UiTheme.TextMuted,
                new Constraints
                {
                    Width = new Fixed(labelWidth),
                    Height = new Fill(),
                }
            );
            ui.Label(
                value,
                UiMetrics.MENU_LABEL_FONT,
                UiTheme.TextSoft,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            );
        }
    }

    /// <summary>The Continue button state, resolved from the save listing and the recorded profile summary.</summary>
    /// <summary>The menu button states the page reacts to after drawing the column.</summary>
    private readonly record struct MenuColumnResult(bool ContinueHovered, bool SingleplayerClicked);

    /// <summary>The Continue button state, resolved from the save listing and the profile summary.</summary>
    private readonly record struct ContinueState(
        ContinueDetails Details,
        bool HasLastSession,
        Character? LastCharacter,
        bool Remote,
        string? Destination
    );

    /// <summary>The archive facts the Continue card shows, from the live listing or the recorded summary.</summary>
    /// <summary>The card facts the Continue hover card draws.</summary>
    private readonly record struct ContinueDetails(
        string Name,
        GameMode Mode,
        int Seed,
        long LastPlayedMs,
        long AgeMs
    );
}
