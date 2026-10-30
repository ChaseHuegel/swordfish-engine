using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.Statistics;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class SelectCharacterPage : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectCharacter;

    private readonly CharacterSaveManager _characterSaveManager;
    private readonly ICharacterStorage _characterStorage;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly CharacterAssetService _characterAssetService;
    private readonly GameSaveManager _gameSaveManager;
    private readonly ISkillDatabase _skillDatabase;
    private readonly ModalMenu _modalMenu;
    private readonly ConfirmModal _confirmModal;
    private readonly ScrollAreaState _scroll = new();
    private readonly SelectCharacterView _view;
    private readonly List<Character> _characters = [];

    private Menu<MenuPage>? _menu;
    private int _narrowTab;

    public SelectCharacterPage(
        in CharacterSaveManager characterSaveManager,
        in ICharacterStorage characterStorage,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in CharacterAssetService characterAssetService,
        in GameSaveManager gameSaveManager,
        in ISkillDatabase skillDatabase,
        in ModalMenu modalMenu,
        in ConfirmModal confirmModal
    )
    {
        _characterSaveManager = characterSaveManager;
        _characterStorage = characterStorage;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _characterAssetService = characterAssetService;
        _gameSaveManager = gameSaveManager;
        _skillDatabase = skillDatabase;
        _modalMenu = modalMenu;
        _confirmModal = confirmModal;

        _view = new SelectCharacterView(localization, _scroll)
        {
            CharacterSelected = SelectCharacter,
            CharacterDeleteRequested = RequestDelete,
            NewCharacterRequested = NewCharacter,
            TabRequested = SelectTab,
            BackRequested = Back,
            LaunchRequested = Launch,
        };
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        _menu = menu;
        _characterSaveManager.ActiveSave ??= _characterSaveManager.GetMostRecentSave();

        ElementBounds viewport = ui.GetElementBounds("SelectCharacter.Manifest");
        ElementBounds content = ui.GetElementBounds("SelectCharacter.List");
        if (viewport.Rect.Size.Y > 0 && content.Rect.Size.Y > 0)
        {
            _scroll.SetMetrics(viewport.Rect.Size.Y, content.Rect.Size.Y);
        }

        ElementBounds narrowViewport = ui.GetElementBounds("SelectCharacter.NarrowManifest");
        ElementBounds narrowContent = ui.GetElementBounds("SelectCharacter.NarrowList");
        if (narrowViewport.Rect.Size.Y > 0 && narrowContent.Rect.Size.Y > 0)
        {
            _scroll.SetMetrics(narrowViewport.Rect.Size.Y, narrowContent.Rect.Size.Y);
        }

        _view.Render(ui, BuildState(ui.Height));
        return Result.FromSuccess();
    }

    /// <summary>Collects the crew rows and detail readouts shown this frame.</summary>
    private SelectCharacterState BuildState(int windowHeight)
    {
        RefreshCharacters();
        Character? active = _characterSaveManager.ActiveSave ?? _characterSaveManager.GetMostRecentSave();

        var rows = new List<CharacterRowState>(_characters.Count);
        foreach (Character character in _characters)
        {
            rows.Add(new CharacterRowState(
                character.Uuid.ToString(),
                character.Name,
                active != null && active.Value.Uuid == character.Uuid,
                _characterAssetService.GetAppearanceMaterial(character)
            ));
        }

        var detail = default(CharacterDetailState);
        if (active != null)
        {
            Character character = active.Value;
            int level = character.GetTotalSkillLevel(_skillDatabase);
            detail = new CharacterDetailState(
                character.Name,
                _view.LevelLine(level, _characterAssetService.GetSpecies(character.Body)),
                _view.Labeled("ui.label.lastPlayed", _view.LastPlayedText(character.Playtime.LastPlayedMs)),
                _view.Labeled("ui.label.timePlayed", _view.TimePlayedText(character.Playtime.AgeMs)),
                _characterAssetService.GetAppearanceMaterial(character),
                BuildStats(character),
                _gameSaveManager.ActiveSave?.Name ?? _localization.GetRequired("ui.label.none")
            );
        }

        bool canLaunch = active != null && _gameSaveManager.ActiveSave != null;
        return new SelectCharacterState(
            _characters.Count > 0,
            active != null,
            canLaunch,
            windowHeight >= 720,
            _narrowTab,
            rows,
            detail
        );
    }

    /// <summary>Builds the six primary stat rows of the detail.</summary>
    private List<StatRowState> BuildStats(in Character character)
    {
        return
        [
            new StatRowState(_localization.GetRequired("ui.label.character.strength"), character.Strength.ToString()),
            new StatRowState(_localization.GetRequired("ui.label.character.precision"), character.Precision.ToString()),
            new StatRowState(_localization.GetRequired("ui.label.character.awareness"), character.Awareness.ToString()),
            new StatRowState(_localization.GetRequired("ui.label.character.charisma"), character.Charisma.ToString()),
            new StatRowState(_localization.GetRequired("ui.label.character.education"), character.Education.ToString()),
            new StatRowState(_localization.GetRequired("ui.label.character.resolve"), character.Resolve.ToString()),
        ];
    }

    private void SelectCharacter(string uuid)
    {
        if (!ulong.TryParse(uuid, out ulong id))
        {
            return;
        }

        foreach (Character character in _characters)
        {
            if (character.Uuid != id)
            {
                continue;
            }

            _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
            _characterSaveManager.ActiveSave = character;
            return;
        }
    }

    private void SelectTab(int tab)
    {
        _narrowTab = tab;
    }

    private void NewCharacter()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        _menu?.GoToPage(MenuPage.NewCharacter);
    }

    private void Back()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        _menu?.GoToPage(MenuPage.Home);
    }

    private void Launch()
    {
        if (_characterSaveManager.ActiveSave == null || _gameSaveManager.ActiveSave == null)
        {
            return;
        }

        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        Task.Run(_gameSaveManager.Load);
    }

    /// <summary>Opens the delete confirmation for one colonist.</summary>
    private void RequestDelete(string uuid)
    {
        if (!ulong.TryParse(uuid, out ulong id))
        {
            return;
        }

        Character? target = null;
        foreach (Character character in _characters)
        {
            if (character.Uuid == id)
            {
                target = character;
                break;
            }
        }

        if (target == null)
        {
            return;
        }

        Character character1 = target.Value;
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        _characterSaveManager.ActiveSave = character1;
        _modalMenu.GoToPage(_confirmModal.Create(
            () => DeleteCharacter(character1),
            _localization.GetRequired("ui.dialog.character.delete"),
            _localization.GetRequired("ui.dialog.character.delete.body"),
            _localization.GetRequired("ui.button.character.delete"),
            _localization.GetRequired("ui.button.cancel")
        ));
    }

    /// <summary>Deletes one character and selects the most recent remaining save.</summary>
    private void DeleteCharacter(Character character)
    {
        _characterSaveManager.Delete(character);
        _characterSaveManager.ActiveSave = _characterSaveManager.GetMostRecentSave();
    }

    /// <summary>Reloads the cached character list, ordered by the most recent play first.</summary>
    private void RefreshCharacters()
    {
        _characters.Clear();
        _characters.AddRange(_characterStorage.GetAllCharacters());

        //  Stable insertion sort: the most recently played character leads, and equal
        //  timestamps keep their storage order. List.Sort is not stable, and LINQ is not free here.
        for (var i = 1; i < _characters.Count; i++)
        {
            Character current = _characters[i];
            var j = i - 1;
            while (j >= 0 && _characters[j].Playtime.LastPlayedMs < current.Playtime.LastPlayedMs)
            {
                _characters[j + 1] = _characters[j];
                j--;
            }

            _characters[j + 1] = current;
        }
    }
}
