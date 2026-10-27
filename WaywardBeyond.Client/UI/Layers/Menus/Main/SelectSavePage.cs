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
    private const int NEW_BUTTON_HEIGHT = 32;
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

        using (ui.Page(layout))
        {
            ui.Padding = new Padding(left: 0, top: MenuLayout.PAGE_GAP, right: 0, bottom: 0);
            ui.Spacing = MenuLayout.PAGE_GAP;

            DrawPanels(ui, layout, menu);
            DrawFooter(ui, menu);
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the tab strip and the panels that the current layout shows.</summary>
    private void DrawPanels(UIBuilder<Material> ui, in MenuLayout layout, Menu<MenuPage> menu)
    {
        if (!layout.UseTabs)
        {
            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                },
                spacing: MenuLayout.PANEL_GAP
            ))
            {
                DrawArchives(
                    ui,
                    new Constraints
                    {
                        Width = new Relative(layout.LeftRatio),
                        Height = new Fill(),
                    },
                    menu
                );
                DrawTelemetry(
                    ui,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    }
                );
            }

            return;
        }

        DrawTabs(ui);
        Constraints pane = new()
        {
            Width = new Fill(),
            Height = new Fill(),
        };

        if (_narrowTab == 0)
        {
            DrawArchives(ui, pane, menu);
            return;
        }

        DrawTelemetry(ui, pane);
    }

    /// <summary>Draws the page footer and runs the action that the player picked.</summary>
    private void DrawFooter(UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        bool remote = _rpcHost.IsRemote;
        string returnLabel = remote
            ? _localization.GetRequired("ui.button.servers")
            : _localization.GetRequired("ui.button.return");

        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(MenuLayout.FOOTER_HEIGHT),
            },
            spacing: 12
        ))
        {
            if (ui.ActionButton(
                "Button_Back",
                returnLabel,
                _returnButton.FooterButton(MenuLayout.RETURN_BUTTON_WIDTH)
            ))
            {
                menu.GoToPage(remote ? MenuPage.Multiplayer : MenuPage.Home);
            }

            ui.Filler();

            GameSave? activeSave = _gameSaveManager.ActiveSave ?? _gameSaveManager.GetMostRecentSave();
            Widgets.ActionButtonOptions nextOptions = _launchButton
                .FooterButton(MenuLayout.PRIMARY_BUTTON_WIDTH) with
            {
                Enabled = activeSave != null,
            };
            if (ui.ActionButton(
                "Button_SelectSave",
                _localization.GetRequired("ui.button.next"),
                nextOptions
            ))
            {
                IReadOnlyList<Character> characters = _characterStorage.GetAllCharacters();
                menu.GoToPage(characters.Count > 0 ? MenuPage.SelectCharacter : MenuPage.NewCharacter);
            }
        }
    }

    private void DrawTabs(UIBuilder<Material> ui)
    {
        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(MenuLayout.TABS_HEIGHT),
            },
            spacing: 8
        ))
        {
            var tabConstraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };

            if (ui.Tab(
                "ArchivesTab",
                _localization.GetRequired("ui.menu.galacticArchives"),
                selected: _narrowTab == 0,
                tabConstraints
            ))
            {
                _narrowTab = 0;
            }

            if (ui.Tab(
                "TelemetryTab",
                _localization.GetRequired("ui.menu.sectorTelemetry"),
                selected: _narrowTab == 1,
                tabConstraints
            ))
            {
                _narrowTab = 1;
            }
        }
    }

    private void DrawArchives(UIBuilder<Material> ui, Constraints constraints, Menu<MenuPage> menu)
    {
        using (ui.MenuPanel(constraints, _localization.GetRequired("ui.menu.galacticArchives")))
        {
            GameSave[] saves = _gameSaveService.GetSaves();

            using (ui.Column(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            ))
            {
                if (saves.Length == 0)
                {
                    ui.Label(
                        _localization.GetRequired("ui.text.none"),
                        UiMetrics.MENU_BODY_FONT,
                        UiTheme.TextDim,
                        new Constraints
                        {
                            Width = new Fill(),
                            Height = new Fill(),
                        },
                        HorizontalAlignment.Center
                    );
                }
                else
                {
                    DrawArchiveList(ui, saves);
                }

                if (_gameSaveService.CanCreateSave)
                {
                    ui.Spacer(height: 8);
                    DrawNewSaveButton(ui, menu);
                }
            }
        }
    }

    /// <summary>Draws the scrolling list of galaxy archive rows.</summary>
    private void DrawArchiveList(UIBuilder<Material> ui, GameSave[] saves)
    {
        int contentHeight = saves.Length * CARD_HEIGHT + Math.Max(0, saves.Length - 1) * CARD_GAP;
        Action<UIBuilder<Material>> body = contentUi => DrawSaveCards(contentUi, saves);

        ui.ScrollArea(
            "SelectSave.Archives",
            contentHeight,
            ref _scrollY,
            _inputService,
            body
        );
    }

    /// <summary>Draws every galaxy archive row inside the scroll area.</summary>
    private void DrawSaveCards(UIBuilder<Material> ui, GameSave[] saves)
    {
        Action<GameSave> deleteSave = DeleteSave;

        for (var i = 0; i < saves.Length; i++)
        {
            if (i > 0)
            {
                ui.Spacer(height: CARD_GAP);
            }

            DrawSaveCard(ui, saves[i], deleteSave);
        }
    }

    /// <summary>Draws one galaxy archive row and applies its selection.</summary>
    private void DrawSaveCard(UIBuilder<Material> ui, GameSave save, Action<GameSave> deleteSave)
    {
        bool selected = _gameSaveManager.ActiveSave != null &&
            _gameSaveManager.ActiveSave.Value.Level.Guid == save.Level.Guid;
        var options = new Widgets.SelectableCardOptions(selected, SoundEffectService: _soundEffectService);
        var context = new SaveCardContext(
            save,
            selected,
            _gameSaveManager,
            _modalMenu,
            _confirmModal,
            _soundEffectService,
            deleteSave
        );

        bool clicked = ui.SelectableCard(
            $"Button_SelectSave_{save.Name}",
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(CARD_HEIGHT),
            },
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
        using (contentUi.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            },
            spacing: 8
        ))
        {
            contentUi.Padding = new Padding(left: 8, top: 0, right: 8, bottom: 0);

            contentUi.Label(
                MenuGlyphs.GALAXY,
                20,
                context.Selected ? UiTheme.AccentBright : UiTheme.Accent,
                new Constraints
                {
                    Width = new Fixed(24),
                    Height = new Fill(),
                },
                HorizontalAlignment.Center,
                fontId: UiTheme.FONT_ICONS
            );
            contentUi.Label(
                context.Save.Name,
                UiMetrics.MENU_BODY_FONT,
                context.Selected ? UiTheme.AccentBright : UiTheme.Text,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            );

            DrawSaveDeleteButton(contentUi, context);
        }
    }

    /// <summary>Draws the delete control of one galaxy archive row and opens its confirmation.</summary>
    private static void DrawSaveDeleteButton(UIBuilder<Material> contentUi, SaveCardContext context)
    {
        string id = $"Button_DeleteSave_{context.Save.Name}";
        var options = new Widgets.IconButtonOptions(
            Danger: true,
            SoundEffectService: context.SoundEffectService
        );
        bool clicked = contentUi.IconButton(
            id,
            MenuGlyphs.TRASH,
            options,
            new Constraints
            {
                Width = new Fixed(28),
                Height = new Fixed(28),
                Anchors = Anchors.Center,
            }
        );
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
    private void DrawNewSaveButton(UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        var options = new Widgets.ActionButtonOptions(
            Variant: ButtonVariant.Subtle,
            Layout: new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(NEW_BUTTON_HEIGHT),
            }
        );
        bool clicked = ui.ActionButton(
            "Button_NewSave",
            "+ " + _localization.GetRequired("ui.menu.chartNewGalaxy"),
            options
        );
        if (clicked)
        {
            menu.GoToPage(MenuPage.NewSave);
        }
    }

    private void DrawTelemetry(UIBuilder<Material> ui, Constraints constraints)
    {
        using (ui.MenuPanel(constraints, _localization.GetRequired("ui.menu.sectorTelemetry")))
        {
            GameSave? activeSave = _gameSaveManager.ActiveSave ?? _gameSaveManager.GetMostRecentSave();
            if (activeSave == null)
            {
                ui.Label(
                    _localization.GetRequired("ui.text.none"),
                    UiMetrics.MENU_BODY_FONT,
                    UiTheme.TextDim,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    },
                    HorizontalAlignment.Center
                );
                return;
            }

            DrawTelemetryReadouts(ui, activeSave.Value);
            DrawTelemetryColonist(ui);
        }
    }

    /// <summary>Draws the name, the mode, the seed, and the playtime readouts of the telemetry panel.</summary>
    private void DrawTelemetryReadouts(UIBuilder<Material> ui, in GameSave save)
    {
        ui.Label(
            save.Name,
            UiMetrics.MENU_TITLE_FONT,
            UiTheme.Text,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(26),
            }
        );
        ui.Label(
            $"{_localization.GetRequired("ui.menu.mode")}: {save.Level.DefaultGameMode}",
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(18),
            }
        );
        ui.Label(
            $"{_localization.GetRequired("ui.menu.seed")}: {save.Level.Seed}",
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(18),
            }
        );

        string lastPlayed = save.Level.Playtime.LastPlayedMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(save.Level.Playtime.LastPlayedMs).ToLocalTime().ToString("g")
            : _localization.GetRequired("ui.label.never");
        ui.Label(
            $"{_localization.GetRequired("ui.label.lastPlayed")}: {lastPlayed}",
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(18),
            }
        );

        TimeSpan age = TimeSpan.FromMilliseconds(save.Level.Playtime.AgeMs);
        ui.Label(
            $"{_localization.GetRequired("ui.label.timePlayed")}: {_localization.GetLongString(age)}",
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(18),
            }
        );
    }

    /// <summary>Draws the active colonist block of the telemetry panel.</summary>
    private void DrawTelemetryColonist(UIBuilder<Material> ui)
    {
        ui.Spacer(height: 12);
        ui.Divider(
            UiTheme.Border,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(1),
            }
        );
        ui.Spacer(height: 8);
        ui.Label(
            _localization.GetRequired("ui.menu.activeColonist"),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(18),
            }
        );

        Character? character = _characterSaveManager.ActiveSave ?? _characterSaveManager.GetMostRecentSave();
        if (character == null)
        {
            ui.Label(
                _localization.GetRequired("ui.menu.noColonist"),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.TextDim,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(20),
                }
            );
            return;
        }

        ui.Spacer(height: 12);
        DrawTelemetryPortrait(ui, character.Value);
    }

    /// <summary>Draws the colonist portrait, name, and level of the telemetry panel.</summary>
    private void DrawTelemetryPortrait(UIBuilder<Material> ui, in Character character)
    {
        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(96),
            },
            spacing: 16
        ))
        {
            Material appearance = _characterAssetService.GetAppearanceMaterial(character);
            using (ui.Image(appearance))
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(96),
                    Height = new Fixed(96),
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
                ui.Label(
                    character.Name,
                    UiMetrics.MENU_BODY_FONT,
                    UiTheme.AccentBright,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fixed(22),
                    }
                );
                int level = character.GetTotalSkillLevel(_skillDatabase);
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
        GameSaveManager GameSaveManager,
        ModalMenu ModalMenu,
        ConfirmModal ConfirmModal,
        SoundEffectService SoundEffectService,
        Action<GameSave> DeleteSave
    );
}
