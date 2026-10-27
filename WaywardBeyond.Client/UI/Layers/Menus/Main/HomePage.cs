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

        if (_continueService.TryDequeueFailure(out ContinueService.Failure failure))
        {
            _modalMenu.GoToPage(_messageModal.Create(failure.Title, failure.Body));
        }

        GameSave[] saves = _gameSaveService.GetSaves();
        LastServerMode lastMode = _profileSettings.LastServerMode.Get();

        GameSave? continueSave = null;
        string lastSaveGuid = _profileSettings.LastSaveGuid.Get();
        for (var i = 0; i < saves.Length; i++)
        {
            if (saves[i].Level.Guid == lastSaveGuid)
            {
                continueSave = saves[i];
                break;
            }
        }

        Character? continueCharacter = null;
        string lastCharacterUuid = _profileSettings.LastCharacterUuid.Get();
        IReadOnlyList<Character> characters = _characterStorage.GetAllCharacters();
        for (var i = 0; i < characters.Count; i++)
        {
            if (characters[i].Uuid.ToString() == lastCharacterUuid)
            {
                continueCharacter = characters[i];
                break;
            }
        }

        //  Prefer the live listing, and fall back to the recorded summary when it does not hold the last save.
        ContinueDetails continueDetails = continueSave != null
            ? new ContinueDetails(
                continueSave.Value.Name,
                continueSave.Value.Level.DefaultGameMode,
                continueSave.Value.Level.Seed,
                continueSave.Value.Level.Playtime.LastPlayedMs,
                continueSave.Value.Level.Playtime.AgeMs
            )
            : new ContinueDetails(
                _profileSettings.LastSaveName.Get(),
                _profileSettings.LastSaveGameMode.Get(),
                _profileSettings.LastSaveSeed.Get(),
                _profileSettings.LastSaveLastPlayedMs.Get(),
                _profileSettings.LastSaveAgeMs.Get()
            );

        bool hasLastSession = _continueService.HasLastSession
            && continueCharacter != null
            && !string.IsNullOrWhiteSpace(continueDetails.Name);

        string? continueDestination = null;
        if (lastMode == LastServerMode.Remote)
        {
            string host = _profileSettings.LastServerHost.Get();
            int port = _profileSettings.LastServerPort.Get();
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

            continueDestination = !string.IsNullOrWhiteSpace(name) ? name : $"{host}:{port}";
        }

        int pageWidth = Math.Clamp(ui.Width - 80, 360, MAX_PAGE_WIDTH);
        var singleplayerClicked = false;

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
            int buttonWidth = UiMetrics.MenuButtonMinWidth;
            int buttonHeight = UiMetrics.MenuButtonHeight;
            var y = 0;

            IntRect continueRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
            string continueLabel = _localization.GetString("ui.button.continue")!;
            var continueOptions = _continueButton with { Enabled = hasLastSession };
            if (ui.ActionButton("Button_Continue", continueRect, continueLabel, continueOptions, out bool continueHovered))
            {
                _continueService.Start();
            }

            y += buttonHeight + BUTTON_GAP;
            IntRect singleplayerRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
            string singleplayerLabel = _localization.GetString("ui.button.singleplayer")!;
            if (ui.ActionButton("Button_Singleplayer", singleplayerRect, singleplayerLabel, _newGameButton))
            {
                singleplayerClicked = true;
            }

            y += buttonHeight + BUTTON_GAP;
            IntRect multiplayerRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
            string multiplayerLabel = _localization.GetString("ui.button.multiplayer")!;
            if (ui.ActionButton("Button_Multiplayer", multiplayerRect, multiplayerLabel, _multiplayerButton))
            {
                menu.GoToPage(MenuPage.Multiplayer);
            }

            y += buttonHeight + DIVIDER_GAP;
            ui.DottedDivider(Widgets.Rect(buttonX, y, buttonWidth, DIVIDER_HEIGHT));
            y += DIVIDER_HEIGHT + DIVIDER_GAP;

            IntRect settingsRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
            string settingsLabel = _localization.GetString("ui.button.settings")!;
            if (ui.ActionButton("Button_Settings", settingsRect, settingsLabel, _optionsButton))
            {
                menu.GoToPage(MenuPage.Settings);
            }

            y += buttonHeight + BUTTON_GAP;
            IntRect creditsRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
            string creditsLabel = _localization.GetString("ui.menu.credits")!;
            if (ui.ActionButton("Button_Credits", creditsRect, creditsLabel, _creditsButton))
            {
                menu.GoToPage(MenuPage.Credits);
            }

            y += buttonHeight + BUTTON_GAP;
            IntRect quitRect = Widgets.Rect(buttonX, y, buttonWidth, buttonHeight);
            string quitLabel = _localization.GetString("ui.button.quit")!;
            if (ui.ActionButton("Button_Quit", quitRect, quitLabel, _exitButton))
            {
                _entry.Quit();
            }

            if (continueHovered && hasLastSession && continueCharacter != null)
            {
                DrawContinueCard(
                    ui,
                    continueDetails,
                    continueCharacter.Value,
                    lastMode == LastServerMode.Remote,
                    continueDestination,
                    buttonX + UiMetrics.MenuButtonMinWidth + 24,
                    0,
                    pageWidth - buttonX - UiMetrics.MenuButtonMinWidth - 28
                );
            }
        }

        if (singleplayerClicked)
        {
            StartSingleplayer(menu);
        }

        return Result.FromSuccess();
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
        int x,
        int y,
        int width
    ) {
        if (width < 200)
        {
            return;
        }

        const int padding = 12;
        const int labelWidth = 100;
        int metadataY = remote ? 74 : 52;
        int dividerY = metadataY + 4 * 18 + 8;
        int characterY = dividerY + 12;
        int height = characterY + 64 + 8;

        using (ui.MenuPanel(Widgets.Rect(x, y, width, height)))
        {
            int typeX = padding;
            if (remote)
            {
                ui.Label(
                    MenuGlyphs.Server,
                    Widgets.Rect(padding, 8, 16, 16),
                    UiMetrics.MenuSmallFont,
                    UiTheme.Accent,
                    Anchors.Center,
                    UiTheme.FontIcons
                );
                typeX += 18;
            }

            ui.Label(
                remote
                    ? _localization.GetString("ui.button.multiplayer")!
                    : _localization.GetString("ui.button.singleplayer")!,
                Widgets.Rect(typeX, 8, width - typeX - padding, 16),
                UiMetrics.MenuSmallFont,
                remote ? UiTheme.Accent : UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                details.Name,
                Widgets.Rect(padding, 28, width - padding * 2, 20),
                UiMetrics.MenuBodyFont,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );

            if (remote && !string.IsNullOrWhiteSpace(destination))
            {
                ui.Label(
                    destination,
                    Widgets.Rect(padding, 50, width - padding * 2, 16),
                    UiMetrics.MenuSmallFont,
                    UiTheme.TextMuted,
                    Anchors.Left | Anchors.Center
                );
            }

            string lastPlayed = details.LastPlayedMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(details.LastPlayedMs).ToLocalTime().ToString("d")
                : _localization.GetString("ui.label.never")!;
            TimeSpan age = TimeSpan.FromMilliseconds(details.AgeMs);

            DrawContinueDetail(
                ui,
                padding,
                metadataY,
                width,
                labelWidth,
                _localization.GetString("ui.menu.mode")!,
                details.Mode.ToString()
            );
            DrawContinueDetail(
                ui,
                padding,
                metadataY + 18,
                width,
                labelWidth,
                _localization.GetString("ui.menu.seed")!,
                details.Seed.ToString()
            );
            DrawContinueDetail(
                ui,
                padding,
                metadataY + 36,
                width,
                labelWidth,
                _localization.GetString("ui.label.lastPlayed")!,
                lastPlayed
            );
            DrawContinueDetail(
                ui,
                padding,
                metadataY + 54,
                width,
                labelWidth,
                _localization.GetString("ui.label.timePlayed")!,
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
                UiMetrics.MenuSmallFont,
                UiTheme.AccentBright,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetString("ui.menu.level")!} {level}",
                Widgets.Rect(textX, characterY + 20, width - textX - padding, 18),
                UiMetrics.MenuSmallFont,
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
            UiMetrics.MenuLabelFont,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            value,
            Widgets.Rect(padding + labelWidth, y, width - padding * 2 - labelWidth, 16),
            UiMetrics.MenuLabelFont,
            UiTheme.TextSoft,
            Anchors.Left | Anchors.Center
        );
    }

    /// <summary>The archive facts the Continue card shows, from the live listing or the recorded summary.</summary>
    private readonly record struct ContinueDetails(
        string Name,
        GameMode Mode,
        int Seed,
        long LastPlayedMs,
        long AgeMs
    );
}
