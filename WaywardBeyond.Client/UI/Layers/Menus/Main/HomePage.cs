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
    in ModalMenu modalMenu,
    in SeedPhraseService seedPhraseService
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Home;


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
    private readonly SeedPhraseService _seedPhraseService = seedPhraseService;

    private HomeView? _view;

    private HomeView View => _view ??= CreateView();

    private Menu<MenuPage>? _menu;

    private bool _hasShownNotice;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        _menu = menu;

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
        View.Render(
            ui,
            new HomeState(
                continueState.HasLastSession,
                continueState.HasLastSession ? BuildCard(continueState) : default
            )
        );

        return Result.FromSuccess();
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

    private HomeView CreateView()
    {
        return new HomeView(_localization)
        {
            ContinueRequested = () => _continueService.Start(),
            SingleplayerRequested = StartSingleplayer,
            MultiplayerRequested = () => _menu?.GoToPage(MenuPage.Multiplayer),
            SettingsRequested = () => _menu?.GoToPage(MenuPage.Settings),
            CreditsRequested = () => _menu?.GoToPage(MenuPage.Credits),
            QuitRequested = () => _entry.Quit(),
        };
    }

    /// <summary>Builds the Continue card facts from the resolved last session.</summary>
    private HomeCardState BuildCard(in ContinueState continueState)
    {
        Character character = continueState.LastCharacter!.Value;
        ContinueDetails details = continueState.Details;
        return new HomeCardState(
            continueState.Remote,
            continueState.Destination ?? string.Empty,
            details.Name,
            details.Mode,
            _seedPhraseService.SeedToPhrase(details.Seed),
            details.LastPlayedMs,
            details.AgeMs,
            character.Name,
            _characterAssetService.GetSpecies(character.Body),
            character.GetTotalSkillLevel(_skillDatabase),
            _characterAssetService.GetAppearanceMaterial(character)
        );
    }

    private void StartSingleplayer()
    {
        _rpcHost.Disconnect();
        _gameSaveService.ClearListing();
        _gameSaveManager.ActiveSave = null;
        _menu?.GoToPage(MenuPage.SelectSave);
    }

    /// <summary>The Continue button state, resolved from the save listing and the profile summary.</summary>
    private readonly record struct ContinueState(
        ContinueDetails Details,
        bool HasLastSession,
        Character? LastCharacter,
        bool Remote,
        string? Destination
    );

    /// <summary>The card facts the Continue hover card draws.</summary>
    private readonly record struct ContinueDetails(
        string Name,
        GameMode Mode,
        long Seed,
        long LastPlayedMs,
        long AgeMs
    );
}
