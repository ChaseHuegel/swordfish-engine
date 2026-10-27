using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
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

internal sealed class SelectCharacterPage(
    in CharacterSaveManager characterSaveManager,
    in ICharacterStorage characterStorage,
    in IInputService inputService,
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in CharacterAssetService characterAssetService,
    in GameSaveManager gameSaveManager,
    in ISkillDatabase skillDatabase,
    in ModalMenu modalMenu,
    in ConfirmModal confirmModal
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectCharacter;

    private const int CARD_HEIGHT = 44;
    private const int CARD_GAP = 8;
    private const int PANEL_PADDING = 12;
    private const int NEW_BUTTON_HEIGHT = 32;
    private const int DOSSIER_PREVIEW_Y = 140;

    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;
    private readonly ICharacterStorage _characterStorage = characterStorage;
    private readonly IInputService _inputService = inputService;
    private readonly SoundEffectService _soundEffectService = soundEffectService;
    private readonly ILocalization _localization = localization;
    private readonly CharacterAssetService _characterAssetService = characterAssetService;
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly ISkillDatabase _skillDatabase = skillDatabase;
    private readonly ModalMenu _modalMenu = modalMenu;
    private readonly ConfirmModal _confirmModal = confirmModal;

    private readonly Widgets.ActionButtonOptions _returnButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Secondary,
        soundEffectService
    );
    private readonly Widgets.ActionButtonOptions _launchButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Primary,
        soundEffectService
    );
    private readonly List<Character> _characters = [];
    private int _scrollY;
    private int _narrowTab;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        _characterSaveManager.ActiveSave ??= _characterSaveManager.GetMostRecentSave();

        MenuLayout layout = MenuLayout.Compute(
            ui.Width,
            ui.Height,
            maxPageWidth: 1160,
            twoColumn: true,
            leftRatio: 0.37f
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

            DrawPanels(ui, layout, menu);
            DrawFooter(ui, layout, menu);
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the tab strip and the panels that the current layout shows.</summary>
    private void DrawPanels(UIBuilder<Material> ui, in MenuLayout layout, Menu<MenuPage> menu)
    {
        if (!layout.UseTabs)
        {
            DrawManifest(ui, layout.Left, menu);
            DrawDossier(ui, layout.Right);
            return;
        }

        DrawTabs(ui, layout);
        if (_narrowTab == 0)
        {
            DrawManifest(ui, layout.Right, menu);
            return;
        }

        DrawDossier(ui, layout.Right);
    }

    /// <summary>Draws the page footer and runs the action that the player picked.</summary>
    private void DrawFooter(UIBuilder<Material> ui, in MenuLayout layout, Menu<MenuPage> menu)
    {
        bool canLaunch = _characterSaveManager.ActiveSave != null && _gameSaveManager.ActiveSave != null;
        var back = new MenuLayout.MenuFooterButton(
            "Button_Back",
            _localization.GetRequired("ui.button.return"),
            _returnButton
        );
        var primary = new MenuLayout.MenuFooterButton(
            "Button_LoadSave",
            _localization.GetRequired("ui.button.loadSave"),
            _launchButton with { Enabled = canLaunch }
        );

        MenuLayout.MenuFooterResult footer = layout.DrawFooter(ui, null, back, primary);
        if (footer.BackClicked)
        {
            menu.GoToPage(MenuPage.Home);
        }

        if (footer.PrimaryClicked)
        {
            Task.Run(_gameSaveManager.Load);
        }
    }

    private void DrawTabs(UIBuilder<Material> ui, in MenuLayout layout)
    {
        int tabWidth = layout.PageWidth / 2;
        IntRect manifestRect = Widgets.Rect(0, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y);
        bool manifestClicked = ui.Tab(
            "ManifestTab",
            manifestRect,
            _localization.GetRequired("ui.menu.crewManifest"),
            selected: _narrowTab == 0
        );
        if (manifestClicked)
        {
            _narrowTab = 0;
        }

        IntRect dossierRect = Widgets.Rect(tabWidth + 4, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y);
        bool dossierClicked = ui.Tab(
            "DossierTab",
            dossierRect,
            _localization.GetRequired("ui.menu.colonistDossier"),
            selected: _narrowTab == 1
        );
        if (dossierClicked)
        {
            _narrowTab = 1;
        }
    }

    private void DrawManifest(UIBuilder<Material> ui, IntRect rect, Menu<MenuPage> menu)
    {
        using (ui.MenuPanel(rect, _localization.GetRequired("ui.menu.crewManifest")))
        {
            RefreshCharacters();

            if (_characterSaveManager.ActiveSave == null)
            {
                _characterSaveManager.ActiveSave = _characterSaveManager.GetMostRecentSave();
            }

            int contentWidth = Math.Max(1, rect.Size.X - PANEL_PADDING * 2);
            int listY = 38;

            if (_characters.Count > 0)
            {
                DrawCharacterList(ui, rect, contentWidth, listY);
            }
            else
            {
                ui.Label(
                    _localization.GetRequired("ui.text.none"),
                    Widgets.Rect(PANEL_PADDING, listY + 12, contentWidth, 20),
                    UiMetrics.MENU_BODY_FONT,
                    UiTheme.TextDim,
                    Anchors.Center
                );
            }

            DrawNewCharacterButton(ui, rect, contentWidth, menu);
        }
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

    /// <summary>Draws the scrolling list of crew manifest rows.</summary>
    private void DrawCharacterList(UIBuilder<Material> ui, IntRect rect, int contentWidth, int listY)
    {
        int listHeight = Math.Max(0, rect.Size.Y - listY - NEW_BUTTON_HEIGHT - PANEL_PADDING - 8);
        IntRect listRect = Widgets.Rect(PANEL_PADDING, listY, contentWidth, listHeight);
        IntRect scrollbarRect = Widgets.Rect(rect.Size.X - PANEL_PADDING - 6, listY, 6, listHeight);
        int contentHeight = _characters.Count * (CARD_HEIGHT + CARD_GAP);
        Action<UIBuilder<Material>> body = contentUi => DrawCharacterCards(contentUi, contentWidth);

        ui.ScrollArea(listRect, contentHeight, ref _scrollY, _inputService, scrollbarRect, body);
    }

    /// <summary>Draws every crew manifest row inside the scroll area.</summary>
    private void DrawCharacterCards(UIBuilder<Material> ui, int contentWidth)
    {
        Action<Character> deleteCharacter = DeleteCharacter;

        for (var i = 0; i < _characters.Count; i++)
        {
            DrawCharacterCard(ui, _characters[i], i, contentWidth, deleteCharacter);
        }
    }

    /// <summary>Draws one crew manifest row and applies its selection.</summary>
    private void DrawCharacterCard(
        UIBuilder<Material> ui,
        Character character,
        int index,
        int contentWidth,
        Action<Character> deleteCharacter
    )
    {
        bool selected = _characterSaveManager.ActiveSave != null &&
            _characterSaveManager.ActiveSave.Value.Uuid == character.Uuid;
        int cardY = index * (CARD_HEIGHT + CARD_GAP);
        IntRect cardRect = Widgets.Rect(0, cardY, contentWidth - 8, CARD_HEIGHT);
        var options = new Widgets.SelectableCardOptions(selected, SoundEffectService: _soundEffectService);
        var context = new CharacterCardContext(
            character,
            selected,
            contentWidth,
            _modalMenu,
            _confirmModal,
            _characterSaveManager,
            _characterAssetService,
            _soundEffectService,
            deleteCharacter
        );

        bool clicked = ui.SelectableCard(
            $"Button_SelectCharacter_{character.Uuid}",
            cardRect,
            in options,
            context,
            static (contentUi, state) => DrawCharacterCardContent(contentUi, state)
        );

        if (clicked)
        {
            _characterSaveManager.ActiveSave = character;
        }
    }

    /// <summary>Draws the portrait, the name, and the delete control of one crew manifest row.</summary>
    private static void DrawCharacterCardContent(UIBuilder<Material> contentUi, CharacterCardContext context)
    {
        Material appearance = context.CharacterAssetService.GetAppearanceMaterial(context.Character);
        using (contentUi.Image(appearance))
        {
            contentUi.Constraints = new Constraints
            {
                X = new Fixed(6),
                Y = new Fixed(6),
                Width = new Fixed(32),
                Height = new Fixed(32),
            };
        }

        contentUi.Label(
            context.Character.Name,
            Widgets.Rect(44, 0, Math.Max(1, context.ContentWidth - 90), CARD_HEIGHT),
            UiMetrics.MENU_BODY_FONT,
            context.Selected ? UiTheme.AccentBright : UiTheme.Text,
            Anchors.Left | Anchors.Center
        );

        DrawCharacterDeleteButton(contentUi, context);
    }

    /// <summary>Draws the delete control of one crew manifest row and opens its confirmation.</summary>
    private static void DrawCharacterDeleteButton(UIBuilder<Material> contentUi, CharacterCardContext context)
    {
        string id = $"Button_DeleteCharacter_{context.Character.Uuid}";
        IntRect rect = Widgets.Rect(context.ContentWidth - 42, 8, 28, 28);
        var options = new Widgets.IconButtonOptions(
            Danger: true,
            Enabled: true,
            SoundEffectService: context.SoundEffectService
        );
        bool clicked = contentUi.IconButton(id, rect, MenuGlyphs.TRASH, options);
        if (!clicked)
        {
            return;
        }

        Character character = context.Character;
        Action<Character> deleteCharacter = context.DeleteCharacter;
        context.CharacterSaveManager.ActiveSave = character;
        context.ModalMenu.GoToPage(context.ConfirmModal.Create(() => deleteCharacter(character)));
    }

    /// <summary>Deletes one character and selects the most recent remaining save.</summary>
    private void DeleteCharacter(Character character)
    {
        _characterSaveManager.Delete(character);
        _characterSaveManager.ActiveSave = _characterSaveManager.GetMostRecentSave();
    }

    /// <summary>Draws the button that opens the new character page.</summary>
    private void DrawNewCharacterButton(
        UIBuilder<Material> ui,
        IntRect rect,
        int contentWidth,
        Menu<MenuPage> menu
    )
    {
        IntRect newButtonRect = Widgets.Rect(
            PANEL_PADDING,
            rect.Size.Y - NEW_BUTTON_HEIGHT - PANEL_PADDING,
            contentWidth,
            NEW_BUTTON_HEIGHT
        );
        bool clicked = ui.ActionButton(
            "Button_NewCharacter",
            newButtonRect,
            "+ " + _localization.GetRequired("ui.button.newCharacter"),
            ButtonVariant.Subtle
        );
        if (clicked)
        {
            menu.GoToPage(MenuPage.NewCharacter);
        }
    }

    private void DrawDossier(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.MenuPanel(rect, _localization.GetRequired("ui.menu.colonistDossier")))
        {
            Character? activeCharacter = _characterSaveManager.ActiveSave;
            if (activeCharacter == null)
            {
                ui.Label(
                    _localization.GetRequired("ui.text.none"),
                    Widgets.Rect(0, 44, rect.Size.X, 24),
                    UiMetrics.MENU_BODY_FONT,
                    UiTheme.TextDim,
                    Anchors.Center
                );
                return;
            }

            int padding = 16;
            Character character = activeCharacter.Value;

            DrawDossierHeader(ui, character, rect, padding);
            DrawDossierPreview(ui, character, padding);
            DrawDossierAssignment(ui, rect, padding);
            DrawDossierSummary(ui, rect, padding);
        }
    }

    /// <summary>Draws the name, level, and playtime readouts of the dossier.</summary>
    private void DrawDossierHeader(UIBuilder<Material> ui, in Character character, IntRect rect, int padding)
    {
        ui.Label(
            character.Name,
            Widgets.Rect(padding, 38, rect.Size.X - padding * 2, 26),
            UiMetrics.MENU_TITLE_FONT,
            UiTheme.Text,
            Anchors.Center
        );

        int level = character.GetTotalSkillLevel(_skillDatabase);
        ui.Label(
            $"{_localization.GetRequired("ui.menu.level")} {level}",
            Widgets.Rect(padding, 66, rect.Size.X - padding * 2, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.AccentBright,
            Anchors.Center
        );

        string lastPlayed = character.Playtime.LastPlayedMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(character.Playtime.LastPlayedMs).ToLocalTime().ToString("g")
            : _localization.GetRequired("ui.label.never");
        ui.Label(
            $"{_localization.GetRequired("ui.label.lastPlayed")}: {lastPlayed}",
            Widgets.Rect(padding, 88, rect.Size.X - padding * 2, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            Anchors.Center
        );

        TimeSpan age = TimeSpan.FromMilliseconds(character.Playtime.AgeMs);
        ui.Label(
            $"{_localization.GetRequired("ui.label.timePlayed")}: {_localization.GetLongString(age)}",
            Widgets.Rect(padding, 106, rect.Size.X - padding * 2, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            Anchors.Center
        );
    }

    /// <summary>Draws the portrait and the six primary stats of the dossier.</summary>
    private void DrawDossierPreview(UIBuilder<Material> ui, in Character character, int padding)
    {
        Material appearance = _characterAssetService.GetAppearanceMaterial(character);
        using (ui.Image(appearance))
        {
            ui.Constraints = new Constraints
            {
                X = new Fixed(padding),
                Y = new Fixed(DOSSIER_PREVIEW_Y),
                Width = new Fixed(128),
                Height = new Fixed(128),
            };
        }

        int statsX = padding + 148;
        DrawStats(ui, character, statsX);
    }

    /// <summary>Draws the six primary stat rows of the dossier.</summary>
    private void DrawStats(UIBuilder<Material> ui, in Character character, int x)
    {
        DrawStat(ui, _localization.GetRequired("ui.field.strength"), character.Strength, x, DOSSIER_PREVIEW_Y);
        DrawStat(ui, _localization.GetRequired("ui.field.precision"), character.Precision, x, DOSSIER_PREVIEW_Y + 24);
        DrawStat(ui, _localization.GetRequired("ui.field.awareness"), character.Awareness, x, DOSSIER_PREVIEW_Y + 48);
        DrawStat(ui, _localization.GetRequired("ui.field.charisma"), character.Charisma, x, DOSSIER_PREVIEW_Y + 72);
        DrawStat(ui, _localization.GetRequired("ui.field.education"), character.Education, x, DOSSIER_PREVIEW_Y + 96);
        DrawStat(ui, _localization.GetRequired("ui.field.resolve"), character.Resolve, x, DOSSIER_PREVIEW_Y + 120);
    }

    /// <summary>Draws the assignment block that tall dossier panels show.</summary>
    private void DrawDossierAssignment(UIBuilder<Material> ui, IntRect rect, int padding)
    {
        if (rect.Size.Y <= 440)
        {
            return;
        }

        ui.Divider(
            Widgets.Rect(padding, 296, rect.Size.X - padding * 2, 1),
            UiTheme.Border
        );
        ui.Label(
            _localization.GetRequired("ui.menu.assignment"),
            Widgets.Rect(padding, 308, rect.Size.X - padding * 2, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            _gameSaveManager.ActiveSave?.Name ?? _localization.GetRequired("ui.text.none"),
            Widgets.Rect(padding, 328, rect.Size.X - padding * 2, 22),
            UiMetrics.MENU_BODY_FONT,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );
    }

    /// <summary>Draws the launch summary at the bottom of the dossier.</summary>
    private void DrawDossierSummary(UIBuilder<Material> ui, IntRect rect, int padding)
    {
        int statsBottom = DOSSIER_PREVIEW_Y + 5 * 24 + 18;
        int summaryY = Math.Max(statsBottom + 16, rect.Size.Y - 64);
        ui.Divider(
            Widgets.Rect(padding, summaryY, rect.Size.X - padding * 2, 1),
            UiTheme.Border
        );
        ui.Label(
            _localization.GetRequired("ui.menu.readyForLaunch"),
            Widgets.Rect(padding, summaryY + 12, rect.Size.X - padding * 2, 20),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Success,
            Anchors.Left | Anchors.Center
        );
    }

    private static void DrawStat(
        UIBuilder<Material> ui,
        string label,
        int value,
        int x,
        int y
    )
    {
        ui.StatLabel(
            label,
            Widgets.Rect(x, y, 130, 18),
            UiMetrics.MENU_SMALL_FONT,
            focused: false
        );
        ui.Label(
            value.ToString(),
            Widgets.Rect(x + 140, y, 28, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Text,
            Anchors.Right | Anchors.Center
        );
    }

    /// <summary>Everything one crew manifest row needs to draw itself.</summary>
    private readonly record struct CharacterCardContext(
        Character Character,
        bool Selected,
        int ContentWidth,
        ModalMenu ModalMenu,
        ConfirmModal ConfirmModal,
        CharacterSaveManager CharacterSaveManager,
        CharacterAssetService CharacterAssetService,
        SoundEffectService SoundEffectService,
        Action<Character> DeleteCharacter
    );
}
