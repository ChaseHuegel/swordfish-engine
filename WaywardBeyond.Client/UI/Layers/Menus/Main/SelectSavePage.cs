using System;
using System.Collections.Generic;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class SelectSavePage : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectSave;

    private const int REFRESH_INTERVAL_MS = 1000;

    private readonly GameSaveManager _gameSaveManager;
    private readonly GameSaveService _gameSaveService;
    private readonly ILocalization _localization;
    private readonly ICharacterStorage _characterStorage;
    private readonly ModalMenu _modalMenu;
    private readonly ConfirmModal _confirmModal;
    private readonly SoundEffectService _soundEffectService;
    private readonly ClientRpcHost _rpcHost;
    private readonly SeedPhraseService _seedPhraseService;
    private readonly ScrollAreaState _scroll = new();

    private readonly SelectSaveView _view;

    private Menu<MenuPage>? _menu;

    private long _lastRenderMs;
    private long _lastRefreshMs;
    private int _narrowTab;

    public SelectSavePage(
        in GameSaveManager gameSaveManager,
        in GameSaveService gameSaveService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in ICharacterStorage characterStorage,
        in ClientRpcHost rpcHost,
        in ModalMenu modalMenu,
        in ConfirmModal confirmModal,
        in SeedPhraseService seedPhraseService
    )
    {
        _gameSaveManager = gameSaveManager;
        _gameSaveService = gameSaveService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _characterStorage = characterStorage;
        _rpcHost = rpcHost;
        _modalMenu = modalMenu;
        _confirmModal = confirmModal;
        _seedPhraseService = seedPhraseService;

        _view = new SelectSaveView(localization, _scroll)
        {
            SaveSelected = SelectSave,
            SaveDeleteRequested = RequestDelete,
            NewSaveRequested = NewSave,
            TabRequested = SelectTab,
            BackRequested = Back,
            NextRequested = Next,
        };
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        _menu = menu;
        RefreshSaveListIfDue();
        _gameSaveManager.ActiveSave ??= _gameSaveManager.GetMostRecentSave();

        ElementBounds viewport = ui.GetElementBounds("SelectSave.Archives");
        ElementBounds content = ui.GetElementBounds("SelectSave.List");
        if (viewport.Rect.Size.Y > 0 && content.Rect.Size.Y > 0)
        {
            _scroll.SetMetrics(viewport.Rect.Size.Y, content.Rect.Size.Y);
        }

        ElementBounds narrowViewport = ui.GetElementBounds("SelectSave.NarrowArchives");
        ElementBounds narrowContent = ui.GetElementBounds("SelectSave.NarrowList");
        if (narrowViewport.Rect.Size.Y > 0 && narrowContent.Rect.Size.Y > 0)
        {
            _scroll.SetMetrics(narrowViewport.Rect.Size.Y, narrowContent.Rect.Size.Y);
        }

        _view.Render(ui, BuildState());
        return Result.FromSuccess();
    }

    /// <summary>Collects the archive rows and telemetry readouts shown this frame.</summary>
    private SelectSaveState BuildState()
    {
        GameSave[] saves = _gameSaveService.GetSaves();
        GameSave? active = _gameSaveManager.ActiveSave ?? _gameSaveManager.GetMostRecentSave();
        var rows = new List<SaveRowState>(saves.Length);
        foreach (GameSave save in saves)
        {
            Level level = save.Level;
            rows.Add(new SaveRowState(
                level.Guid,
                save.Name,
                SelectSaveView.ModeTag(level.DefaultGameMode),
                level.DefaultGameMode == GameMode.Creative,
                _view.LastPlayedLine(level.Playtime.LastPlayedMs),
                active != null && active.Value.Level.Guid == level.Guid
            ));
        }

        var detail = default(SaveDetailState);
        if (active != null)
        {
            Level level = active.Value.Level;
            detail = new SaveDetailState(
                active.Value.Name,
                level.DefaultGameMode.ToString(),
                _seedPhraseService.SeedToPhrase(level.Seed),
                _view.TimePlayedText(level.Playtime.AgeMs),
                _view.LastPlayedText(level.Playtime.LastPlayedMs)
            );
        }

        return new SelectSaveState(
            _rpcHost.IsRemote,
            _gameSaveService.CanCreateSave,
            _narrowTab,
            active != null,
            rows,
            detail
        );
    }

    private void SelectSave(string guid)
    {
        foreach (GameSave save in _gameSaveService.GetSaves())
        {
            if (save.Level.Guid != guid)
            {
                continue;
            }

            _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
            _gameSaveManager.ActiveSave = save;
            return;
        }
    }

    private void SelectTab(int tab)
    {
        _narrowTab = tab;
    }

    private void NewSave()
    {
        Open(MenuPage.NewSave);
    }

    private void Back()
    {
        Open(_rpcHost.IsRemote ? MenuPage.Multiplayer : MenuPage.Home);
    }

    private void Next()
    {
        IReadOnlyList<Character> characters = _characterStorage.GetAllCharacters();
        Open(characters.Count > 0 ? MenuPage.SelectCharacter : MenuPage.NewCharacter);
    }

    private void Open(MenuPage page)
    {
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        _menu?.GoToPage(page);
    }

    /// <summary>Opens the delete confirmation for one archive.</summary>
    private void RequestDelete(string guid)
    {
        GameSave? target = null;
        foreach (GameSave save in _gameSaveService.GetSaves())
        {
            if (save.Level.Guid == guid)
            {
                target = save;
                break;
            }
        }

        if (target == null)
        {
            return;
        }

        GameSave save1 = target.Value;
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        _gameSaveManager.ActiveSave = save1;
        _modalMenu.GoToPage(_confirmModal.Create(
            () => DeleteSave(save1),
            _localization.GetRequired("ui.dialog.level.delete"),
            _localization.GetRequired("ui.dialog.level.delete.body"),
            _localization.GetRequired("ui.button.level.delete"),
            _localization.GetRequired("ui.button.cancel")
        ));
    }

    /// <summary>Deletes one galaxy archive, selects the most recent remaining save, and resets the list scroll.</summary>
    private void DeleteSave(GameSave save)
    {
        _gameSaveManager.Delete(save);
        _gameSaveManager.ActiveSave = _gameSaveManager.GetMostRecentSave();
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
}
