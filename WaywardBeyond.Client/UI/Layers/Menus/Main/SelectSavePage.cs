using System;
using System.Collections.Generic;
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
using WaywardBeyond.Client.Statistics;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class SelectSavePage : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectSave;

    private const int CARD_HEIGHT = 40;
    private const int CARD_GAP = 8;
    private const int PANEL_PADDING = 12;
    private const int NEW_BUTTON_HEIGHT = 32;
    private const int TELEMETRY_PREVIEW_Y = 174;
    private const int REFRESH_INTERVAL_MS = 1000;

    private readonly GameSaveManager _gameSaveManager;
    private readonly GameSaveService _gameSaveService;
    private readonly CharacterSaveManager _characterSaveManager;
    private readonly CharacterAssetService _characterAssetService;
    private readonly ISkillDatabase _skillDatabase;
    private readonly IInputService _inputService;
    private readonly ILocalization _localization;
    private readonly ICharacterStorage _characterStorage;
    private readonly ModalMenu _modalMenu;
    private readonly ConfirmModal _confirmModal;
    private readonly SoundEffectService _soundEffectService;
    private readonly ClientRpcHost _rpcHost;

    private readonly Widgets.ActionButtonOptions _returnButton;
    private readonly Widgets.ActionButtonOptions _launchButton;

    private long _lastRenderMs;
    private long _lastRefreshMs;
    private int _scrollY;
    private int _narrowTab;

    public SelectSavePage(
        in GameSaveManager gameSaveManager,
        in GameSaveService gameSaveService,
        in CharacterSaveManager characterSaveManager,
        in CharacterAssetService characterAssetService,
        in ISkillDatabase skillDatabase,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in ICharacterStorage characterStorage,
        in ClientRpcHost rpcHost,
        in ModalMenu modalMenu,
        in ConfirmModal confirmModal
    )
    {
        _gameSaveManager = gameSaveManager;
        _gameSaveService = gameSaveService;
        _characterSaveManager = characterSaveManager;
        _characterAssetService = characterAssetService;
        _skillDatabase = skillDatabase;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _characterStorage = characterStorage;
        _rpcHost = rpcHost;
        _modalMenu = modalMenu;
        _confirmModal = confirmModal;

        _returnButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);
        _launchButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, soundEffectService);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        RefreshSaveListIfDue();
        _gameSaveManager.ActiveSave ??= _gameSaveManager.GetMostRecentSave();

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
            DrawArchives(ui, layout.Left, menu);
            DrawTelemetry(ui, layout.Right);
            return;
        }

        DrawTabs(ui, layout);
        if (_narrowTab == 0)
        {
            DrawArchives(ui, layout.Right, menu);
            return;
        }

        DrawTelemetry(ui, layout.Right);
    }

    /// <summary>Draws the page footer and runs the action that the player picked.</summary>
    private void DrawFooter(UIBuilder<Material> ui, in MenuLayout layout, Menu<MenuPage> menu)
    {
        bool remote = _rpcHost.IsRemote;
        GameSave? activeSave = _gameSaveManager.ActiveSave ?? _gameSaveManager.GetMostRecentSave();
        var back = new MenuLayout.MenuFooterButton(
            "Button_Back",
            remote
                ? _localization.GetRequired("ui.button.servers")
                : _localization.GetRequired("ui.button.return"),
            _returnButton
        );
        var primary = new MenuLayout.MenuFooterButton(
            "Button_SelectSave",
            _localization.GetRequired("ui.button.next"),
            _launchButton with { Enabled = activeSave != null }
        );

        MenuLayout.MenuFooterResult footer = layout.DrawFooter(ui, null, back, primary);
        if (footer.BackClicked)
        {
            menu.GoToPage(remote ? MenuPage.Multiplayer : MenuPage.Home);
        }

        if (footer.PrimaryClicked)
        {
            IReadOnlyList<Character> characters = _characterStorage.GetAllCharacters();
            menu.GoToPage(characters.Count > 0 ? MenuPage.SelectCharacter : MenuPage.NewCharacter);
        }
    }

    private void DrawTabs(UIBuilder<Material> ui, in MenuLayout layout)
    {
        int tabWidth = layout.PageWidth / 2;
        IntRect archivesRect = Widgets.Rect(0, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y);
        bool archivesClicked = ui.Tab(
            "ArchivesTab",
            archivesRect,
            _localization.GetRequired("ui.menu.galacticArchives"),
            selected: _narrowTab == 0
        );
        if (archivesClicked)
        {
            _narrowTab = 0;
        }

        IntRect telemetryRect = Widgets.Rect(tabWidth + 4, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y);
        bool telemetryClicked = ui.Tab(
            "TelemetryTab",
            telemetryRect,
            _localization.GetRequired("ui.menu.sectorTelemetry"),
            selected: _narrowTab == 1
        );
        if (telemetryClicked)
        {
            _narrowTab = 1;
        }
    }

    private void DrawArchives(UIBuilder<Material> ui, IntRect rect, Menu<MenuPage> menu)
    {
        using (ui.MenuPanel(rect, _localization.GetRequired("ui.menu.galacticArchives")))
        {
            GameSave[] saves = _gameSaveService.GetSaves();
            int contentWidth = Math.Max(1, rect.Size.X - PANEL_PADDING * 2);
            int listY = 38;
            int listHeight = Math.Max(0, rect.Size.Y - listY - NEW_BUTTON_HEIGHT - PANEL_PADDING - 8);

            DrawArchiveList(ui, rect, saves, contentWidth, listY, listHeight);
            DrawNewSaveButton(ui, rect, contentWidth, menu);

            if (saves.Length == 0 && listHeight > 24)
            {
                ui.Label(
                    _localization.GetRequired("ui.text.none"),
                    Widgets.Rect(PANEL_PADDING, listY + 12, contentWidth, 20),
                    UiMetrics.MENU_BODY_FONT,
                    UiTheme.TextDim,
                    Anchors.Center
                );
            }
        }
    }

    /// <summary>Draws the scrolling list of galaxy archive rows.</summary>
    private void DrawArchiveList(
        UIBuilder<Material> ui,
        IntRect rect,
        GameSave[] saves,
        int contentWidth,
        int listY,
        int listHeight
    )
    {
        IntRect listRect = Widgets.Rect(PANEL_PADDING, listY, contentWidth, listHeight);
        IntRect scrollbarRect = Widgets.Rect(rect.Size.X - PANEL_PADDING - 6, listY, 6, listHeight);
        int contentHeight = saves.Length * (CARD_HEIGHT + CARD_GAP);
        Action<UIBuilder<Material>> body = contentUi => DrawSaveCards(contentUi, saves, contentWidth);

        ui.ScrollArea(listRect, contentHeight, ref _scrollY, _inputService, scrollbarRect, body);
    }

    /// <summary>Draws every galaxy archive row inside the scroll area.</summary>
    private void DrawSaveCards(UIBuilder<Material> ui, GameSave[] saves, int contentWidth)
    {
        Action<GameSave> deleteSave = DeleteSave;

        for (var i = 0; i < saves.Length; i++)
        {
            DrawSaveCard(ui, saves[i], i, contentWidth, deleteSave);
        }
    }

    /// <summary>Draws one galaxy archive row and applies its selection.</summary>
    private void DrawSaveCard(
        UIBuilder<Material> ui,
        GameSave save,
        int index,
        int contentWidth,
        Action<GameSave> deleteSave
    )
    {
        bool selected = _gameSaveManager.ActiveSave != null &&
            _gameSaveManager.ActiveSave.Value.Level.Guid == save.Level.Guid;
        int cardY = index * (CARD_HEIGHT + CARD_GAP);
        IntRect cardRect = Widgets.Rect(0, cardY, contentWidth, CARD_HEIGHT);
        var options = new Widgets.SelectableCardOptions(selected, SoundEffectService: _soundEffectService);
        var context = new SaveCardContext(
            save,
            selected,
            contentWidth,
            _gameSaveManager,
            _modalMenu,
            _confirmModal,
            _soundEffectService,
            deleteSave
        );

        bool clicked = ui.SelectableCard(
            $"Button_SelectSave_{save.Name}",
            cardRect,
            in options,
            context,
            static (contentUi, state) => DrawSaveCardContent(contentUi, state)
        );

        if (clicked)
        {
            _gameSaveManager.ActiveSave = save;
        }
    }

    /// <summary>Draws the glyph, the name, and the delete control of one galaxy archive row.</summary>
    private static void DrawSaveCardContent(UIBuilder<Material> contentUi, SaveCardContext context)
    {
        contentUi.Label(
            MenuGlyphs.GALAXY,
            Widgets.Rect(8, 8, 24, 24),
            20,
            context.Selected ? UiTheme.AccentBright : UiTheme.Accent,
            Anchors.Center,
            UiTheme.FONT_ICONS
        );
        contentUi.Label(
            context.Save.Name,
            Widgets.Rect(36, 0, Math.Max(1, context.ContentWidth - 80), CARD_HEIGHT),
            UiMetrics.MENU_BODY_FONT,
            context.Selected ? UiTheme.AccentBright : UiTheme.Text,
            Anchors.Left | Anchors.Center
        );

        DrawSaveDeleteButton(contentUi, context);
    }

    /// <summary>Draws the delete control of one galaxy archive row and opens its confirmation.</summary>
    private static void DrawSaveDeleteButton(UIBuilder<Material> contentUi, SaveCardContext context)
    {
        string id = $"Button_DeleteSave_{context.Save.Name}";
        IntRect rect = Widgets.Rect(context.ContentWidth - 34, 6, 28, 28);
        var options = new Widgets.IconButtonOptions(
            Danger: true,
            SoundEffectService: context.SoundEffectService
        );
        bool clicked = contentUi.IconButton(id, rect, MenuGlyphs.TRASH, options);
        if (!clicked)
        {
            return;
        }

        GameSave save = context.Save;
        Action<GameSave> deleteSave = context.DeleteSave;
        context.GameSaveManager.ActiveSave = save;
        context.ModalMenu.GoToPage(context.ConfirmModal.Create(() => deleteSave(save)));
    }

    /// <summary>Deletes one galaxy archive, selects the most recent remaining save, and resets the list scroll.</summary>
    private void DeleteSave(GameSave save)
    {
        _gameSaveManager.Delete(save);
        _gameSaveManager.ActiveSave = _gameSaveManager.GetMostRecentSave();
        _scrollY = 0;
    }

    /// <summary>Draws the button that opens the new galaxy page.</summary>
    private void DrawNewSaveButton(
        UIBuilder<Material> ui,
        IntRect rect,
        int contentWidth,
        Menu<MenuPage> menu
    )
    {
        if (!_gameSaveService.CanCreateSave)
        {
            return;
        }

        IntRect newButtonRect = Widgets.Rect(
            PANEL_PADDING,
            rect.Size.Y - NEW_BUTTON_HEIGHT - PANEL_PADDING,
            contentWidth,
            NEW_BUTTON_HEIGHT
        );
        bool clicked = ui.ActionButton(
            "Button_NewSave",
            newButtonRect,
            "+ " + _localization.GetRequired("ui.menu.chartNewGalaxy"),
            ButtonVariant.Subtle
        );
        if (clicked)
        {
            menu.GoToPage(MenuPage.NewSave);
        }
    }

    private void DrawTelemetry(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.MenuPanel(rect, _localization.GetRequired("ui.menu.sectorTelemetry")))
        {
            GameSave? activeSave = _gameSaveManager.ActiveSave ?? _gameSaveManager.GetMostRecentSave();
            if (activeSave == null)
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
            DrawTelemetryReadouts(ui, activeSave.Value, rect, padding);
            DrawTelemetryColonist(ui, rect, padding);
            DrawTelemetryRule(ui, rect, padding);
        }
    }

    /// <summary>Draws the name, the mode, the seed, and the playtime readouts of the telemetry panel.</summary>
    private void DrawTelemetryReadouts(UIBuilder<Material> ui, in GameSave save, IntRect rect, int padding)
    {
        ui.Label(
            save.Name,
            Widgets.Rect(padding, 38, rect.Size.X - padding * 2, 24),
            UiMetrics.MENU_TITLE_FONT,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            $"{_localization.GetRequired("ui.menu.mode")}: {save.Level.DefaultGameMode}",
            Widgets.Rect(padding, 70, rect.Size.X - padding * 2, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            $"{_localization.GetRequired("ui.menu.seed")}: {save.Level.Seed}",
            Widgets.Rect(padding, 88, rect.Size.X - padding * 2, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );

        string lastPlayed = save.Level.Playtime.LastPlayedMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(save.Level.Playtime.LastPlayedMs).ToLocalTime().ToString("g")
            : _localization.GetRequired("ui.label.never");
        ui.Label(
            $"{_localization.GetRequired("ui.label.lastPlayed")}: {lastPlayed}",
            Widgets.Rect(padding, 106, rect.Size.X - padding * 2, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );

        TimeSpan age = TimeSpan.FromMilliseconds(save.Level.Playtime.AgeMs);
        ui.Label(
            $"{_localization.GetRequired("ui.label.timePlayed")}: {_localization.GetLongString(age)}",
            Widgets.Rect(padding, 124, rect.Size.X - padding * 2, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
    }

    /// <summary>Draws the active colonist block of the telemetry panel.</summary>
    private void DrawTelemetryColonist(UIBuilder<Material> ui, IntRect rect, int padding)
    {
        ui.Divider(
            Widgets.Rect(padding, 148, rect.Size.X - padding * 2, 1),
            UiTheme.Border
        );
        ui.Label(
            _localization.GetRequired("ui.menu.activeColonist"),
            Widgets.Rect(padding, 152, rect.Size.X - padding * 2, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );

        Character? character = _characterSaveManager.ActiveSave ?? _characterSaveManager.GetMostRecentSave();
        if (character == null)
        {
            ui.Label(
                _localization.GetRequired("ui.menu.noColonist"),
                Widgets.Rect(padding, TELEMETRY_PREVIEW_Y + 16, rect.Size.X - padding * 2, 20),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.TextDim,
                Anchors.Left | Anchors.Center
            );
            return;
        }

        DrawTelemetryPortrait(ui, character.Value, rect, padding);
    }

    /// <summary>Draws the colonist portrait, name, and level of the telemetry panel.</summary>
    private void DrawTelemetryPortrait(UIBuilder<Material> ui, in Character character, IntRect rect, int padding)
    {
        Material appearance = _characterAssetService.GetAppearanceMaterial(character);
        using (ui.Image(appearance))
        {
            ui.Constraints = new Constraints
            {
                X = new Fixed(padding),
                Y = new Fixed(TELEMETRY_PREVIEW_Y),
                Width = new Fixed(96),
                Height = new Fixed(96),
            };
        }

        int statsX = padding + 112;
        ui.Label(
            character.Name,
            Widgets.Rect(statsX, TELEMETRY_PREVIEW_Y, rect.Size.X - statsX - padding, 22),
            UiMetrics.MENU_BODY_FONT,
            UiTheme.AccentBright,
            Anchors.Left | Anchors.Center
        );
        int level = character.GetTotalSkillLevel(_skillDatabase);
        ui.Label(
            $"{_localization.GetRequired("ui.menu.level")} {level}",
            Widgets.Rect(statsX, TELEMETRY_PREVIEW_Y + 28, rect.Size.X - statsX - padding, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );
    }

    /// <summary>Draws the footer rule that a tall telemetry panel shows.</summary>
    private void DrawTelemetryRule(UIBuilder<Material> ui, IntRect rect, int padding)
    {
        if (rect.Size.Y <= 430)
        {
            return;
        }

        ui.Divider(
            Widgets.Rect(padding, 280, rect.Size.X - padding * 2, 1),
            UiTheme.Border
        );
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

    /// <summary>Everything one galaxy archive row needs to draw itself.</summary>
    private readonly record struct SaveCardContext(
        GameSave Save,
        bool Selected,
        int ContentWidth,
        GameSaveManager GameSaveManager,
        ModalMenu ModalMenu,
        ConfirmModal ConfirmModal,
        SoundEffectService SoundEffectService,
        Action<GameSave> DeleteSave
    );
}
