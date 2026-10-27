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
        MenuColumnResult buttons = default;

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

            int buttonX = (pageWidth - UiMetrics.MENU_BUTTON_MIN_WIDTH) / 2;
            buttons = DrawMenuButtons(ui, buttonX, menu, continueState);
            if (buttons.ContinueHovered && continueState.HasLastSession && continueState.LastCharacter != null)
            {
                IntRect cardRect = ContinueCardRect(buttonX, pageWidth, continueState.Remote);
                DrawContinueCard(
                    ui,
                    continueState.Details,
                    continueState.LastCharacter.Value,
                    continueState.Remote,
                    continueState.Destination,
                    cardRect
                );
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
        int buttonX,
        Menu<MenuPage> menu,
        in ContinueState continueState
    )
    {
        int buttonWidth = UiMetrics.MENU_BUTTON_MIN_WIDTH;
        int buttonHeight = UiMetrics.MENU_BUTTON_HEIGHT;
        var y = 0;

        IntRect continueRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
        var continueOptions = _continueButton with { Enabled = continueState.HasLastSession };
        bool continueClicked = ui.ActionButton(
            "Button_Continue",
            continueRect,
            _localization.GetRequired("ui.button.continue"),
            continueOptions,
            out bool continueHovered
        );
        if (continueClicked)
        {
            _continueService.Start();
        }

        y += buttonHeight + BUTTON_GAP;
        IntRect singleplayerRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
        bool singleplayerClicked = ui.ActionButton(
            "Button_Singleplayer",
            singleplayerRect,
            _localization.GetRequired("ui.button.singleplayer"),
            _newGameButton
        );

        y += buttonHeight + BUTTON_GAP;
        IntRect multiplayerRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
        if (ui.ActionButton(
                "Button_Multiplayer",
                multiplayerRect,
                _localization.GetRequired("ui.button.multiplayer"),
                _multiplayerButton
            ))
        {
            menu.GoToPage(MenuPage.Multiplayer);
        }

        y += buttonHeight + DIVIDER_GAP;
        ui.DottedDivider(Widgets.Rect(buttonX, y, buttonWidth, DIVIDER_HEIGHT));
        y += DIVIDER_HEIGHT + DIVIDER_GAP;

        IntRect settingsRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
        if (ui.ActionButton(
                "Button_Settings",
                settingsRect,
                _localization.GetRequired("ui.button.settings"),
                _optionsButton
            ))
        {
            menu.GoToPage(MenuPage.Settings);
        }

        y += buttonHeight + BUTTON_GAP;
        IntRect creditsRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
        if (ui.ActionButton(
                "Button_Credits",
                creditsRect,
                _localization.GetRequired("ui.menu.credits"),
                _creditsButton
            ))
        {
            menu.GoToPage(MenuPage.Credits);
        }

        y += buttonHeight + BUTTON_GAP;
        IntRect quitRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
        if (ui.ActionButton(
                "Button_Quit",
                quitRect,
                _localization.GetRequired("ui.button.quit"),
                _exitButton
            ))
        {
            _entry.Quit();
        }

        return new MenuColumnResult(continueHovered, singleplayerClicked);
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

    /// <summary>Computes the rectangle the Continue card occupies beside the menu column.</summary>
    private static IntRect ContinueCardRect(int buttonX, int pageWidth, bool remote)
    {
        int metadataY = remote ? CONTINUE_CARD_METADATA_TOP_REMOTE : CONTINUE_CARD_METADATA_TOP;
        int height = metadataY
            + CONTINUE_CARD_METADATA_HEIGHT
            + CONTINUE_CARD_DIVIDER_GAP
            + CONTINUE_CARD_CHARACTER_HEIGHT;

        return Widgets.Rect(
            buttonX + UiMetrics.MENU_BUTTON_MIN_WIDTH + CONTINUE_CARD_GAP,
            0,
            pageWidth - buttonX - UiMetrics.MENU_BUTTON_MIN_WIDTH - CONTINUE_CARD_MARGIN,
            height
        );
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
        IntRect rect
    )
    {
        if (rect.Size.X < CONTINUE_CARD_MIN_WIDTH)
        {
            return;
        }

        const int padding = 12;
        const int labelWidth = 100;
        int width = rect.Size.X;
        int metadataY = remote ? CONTINUE_CARD_METADATA_TOP_REMOTE : CONTINUE_CARD_METADATA_TOP;
        int dividerY = metadataY + CONTINUE_CARD_METADATA_HEIGHT;
        int characterY = dividerY + CONTINUE_CARD_DIVIDER_GAP;

        using (ui.MenuPanel(rect))
        {
            int typeX = padding;
            if (remote)
            {
                ui.Label(
                    MenuGlyphs.SERVER,
                    Widgets.Rect(padding, 8, 16, 16),
                    UiMetrics.MENU_SMALL_FONT,
                    UiTheme.Accent,
                    Anchors.Center,
                    UiTheme.FONT_ICONS
                );
                typeX += 18;
            }

            ui.Label(
                remote
                    ? _localization.GetRequired("ui.button.multiplayer")
                    : _localization.GetRequired("ui.button.singleplayer"),
                Widgets.Rect(typeX, 8, width - typeX - padding, 16),
                UiMetrics.MENU_SMALL_FONT,
                remote ? UiTheme.Accent : UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                details.Name,
                Widgets.Rect(padding, 28, width - padding * 2, 20),
                UiMetrics.MENU_BODY_FONT,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );

            if (remote && !string.IsNullOrWhiteSpace(destination))
            {
                ui.Label(
                    destination,
                    Widgets.Rect(padding, 50, width - padding * 2, 16),
                    UiMetrics.MENU_SMALL_FONT,
                    UiTheme.TextMuted,
                    Anchors.Left | Anchors.Center
                );
            }

            string lastPlayed = details.LastPlayedMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(details.LastPlayedMs).ToLocalTime().ToString("d")
                : _localization.GetRequired("ui.label.never");
            TimeSpan age = TimeSpan.FromMilliseconds(details.AgeMs);

            DrawContinueDetail(
                ui,
                padding,
                metadataY,
                width,
                labelWidth,
                _localization.GetRequired("ui.menu.mode"),
                details.Mode.ToString()
            );
            DrawContinueDetail(
                ui,
                padding,
                metadataY + 18,
                width,
                labelWidth,
                _localization.GetRequired("ui.menu.seed"),
                details.Seed.ToString()
            );
            DrawContinueDetail(
                ui,
                padding,
                metadataY + 36,
                width,
                labelWidth,
                _localization.GetRequired("ui.label.lastPlayed"),
                lastPlayed
            );
            DrawContinueDetail(
                ui,
                padding,
                metadataY + 54,
                width,
                labelWidth,
                _localization.GetRequired("ui.label.timePlayed"),
                _localization.GetLongString(age)
            );

            ui.Divider(
                Widgets.Rect(padding, dividerY, width - padding * 2, 1),
                UiTheme.BorderInner
            );

            Material appearance = _characterAssetService.GetAppearanceMaterial(character);
            using (ui.Image(appearance))
            {
                ui.Constraints = new Constraints
                {
                    X = new Fixed(padding),
                    Y = new Fixed(characterY),
                    Width = new Fixed(64),
                    Height = new Fixed(64),
                };
            }

            int textX = padding + 76;
            int level = character.GetTotalSkillLevel(_skillDatabase);
            ui.Label(
                character.Name,
                Widgets.Rect(textX, characterY, width - textX - padding, 18),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.AccentBright,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetRequired("ui.menu.level")} {level}",
                Widgets.Rect(textX, characterY + 20, width - textX - padding, 18),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
        }
    }

    private static void DrawContinueDetail(
        UIBuilder<Material> ui,
        int padding,
        int y,
        int width,
        int labelWidth,
        string label,
        string value
    )
    {
        ui.Label(
            label,
            Widgets.Rect(padding, y, labelWidth, 16),
            UiMetrics.MENU_LABEL_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            value,
            Widgets.Rect(padding + labelWidth, y, width - padding * 2 - labelWidth, 16),
            UiMetrics.MENU_LABEL_FONT,
            UiTheme.TextSoft,
            Anchors.Left | Anchors.Center
        );
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
