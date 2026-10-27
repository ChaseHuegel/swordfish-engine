using Microsoft.Extensions.Logging;
using Reef.Constraints;
using Reef.UI;
using Reef;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Numerics;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data.Characters;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class NewCharacterPage : IMenuPage<MenuPage>
{
    private const int DEFAULT_SPACER_POINTS = 18;
    private const int MIN_STAT = 1;
    private const int MAX_STAT = 8;
    private const int PANEL_PADDING = 20;
    private const int INPUT_WIDTH = 300;
    private const int BODY_BOX_SIZE = 208;

    private static readonly char[] _characterNameTrimChars = [' ', '\t', '.', '\n', '\r'];

    public MenuPage ID => MenuPage.NewCharacter;

    private readonly ILogger<NewCharacterPage> _logger;
    private readonly ICharacterStorage _characterStorage;
    private readonly CharacterSaveManager _characterSaveManager;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly CharacterAssetService _characterAssetService;
    private readonly NameGenerator _nameGenerator;
    private readonly NotificationService _notificationService;
    private readonly ModalMenu _modalMenu;
    private readonly ConfirmModal _confirmModal;

    private readonly Widgets.ActionButtonOptions _backButton;
    private readonly Widgets.ActionButtonOptions _createButton;
    private readonly Randomizer _randomizer = new();

    private int _characterMaterialIndex;
    private TextBoxState _nameTextBox;
    private int _spacerPoints = DEFAULT_SPACER_POINTS;
    private int _focusedStat;
    private int _narrowTab;
    private int _strength = 1;
    private int _precision = 1;
    private int _awareness = 1;
    private int _charisma = 1;
    private int _education = 1;
    private int _resolve = 1;

    public NewCharacterPage(
        in ILogger<NewCharacterPage> logger,
        in ICharacterStorage characterStorage,
        in CharacterSaveManager characterSaveManager,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in CharacterAssetService characterAssetService,
        in NameGenerator nameGenerator,
        in NotificationService notificationService,
        in ModalMenu modalMenu,
        in ConfirmModal confirmModal
    ) {
        _logger = logger;
        _characterStorage = characterStorage;
        _characterSaveManager = characterSaveManager;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _characterAssetService = characterAssetService;
        _nameGenerator = nameGenerator;
        _notificationService = notificationService;
        _modalMenu = modalMenu;
        _confirmModal = confirmModal;

        _backButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);
        _createButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, soundEffectService);
        var nameTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.characterName"),
            MaxCharacters: 20,
            DisallowedCharacters: ['\0', '\\', '/', ':', '*', '?', '<', '>', '|'],
            Constraints: new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            }
        );
        _nameTextBox = new TextBoxState(initialValue: string.Empty, options: nameTextBoxOptions);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        MenuLayout layout = MenuLayout.Compute(
            ui.Width,
            ui.Height,
            maxPageWidth: 1160,
            twoColumn: true,
            leftRatio: 0.5f
        );

        using (ui.Page(layout))
        {
            ui.Padding = new Padding(left: 0, top: MenuLayout.PAGE_GAP, right: 0, bottom: 0);
            ui.Spacing = MenuLayout.PAGE_GAP;

            DrawPanes(ui, layout);
            DrawFooter(ui, menu);
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the identity and evaluation panes for the current width.</summary>
    private void DrawPanes(UIBuilder<Material> ui, in MenuLayout layout)
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
                DrawIdentity(
                    ui,
                    new Constraints
                    {
                        Width = new Relative(layout.LeftRatio),
                        Height = new Fill(),
                    }
                );
                DrawEvaluation(
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

        using (ui.Column(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            }
        ))
        {
            DrawTabs(ui);
            Constraints pane = new()
            {
                Width = new Fill(),
                Height = new Fill(),
            };

            if (_narrowTab == 0)
            {
                DrawIdentity(ui, pane);
                return;
            }

            DrawEvaluation(ui, pane);
        }
    }

    /// <summary>Draws the narrow-layout tabs.</summary>
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
                "IdentityTab",
                _localization.GetRequired("ui.menu.colonistIdentity"),
                selected: _narrowTab == 0,
                tabConstraints
            ))
            {
                _narrowTab = 0;
            }

            if (ui.Tab(
                "EvaluationTab",
                _localization.GetRequired("ui.menu.spacerEvaluation"),
                selected: _narrowTab == 1,
                tabConstraints
            ))
            {
                _narrowTab = 1;
            }
        }
    }

    /// <summary>Draws the page footer, then opens the created character or leaves the page.</summary>
    private void DrawFooter(UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        string currentName = CurrentName();
        bool nameMissing = string.IsNullOrWhiteSpace(currentName);
        bool nameTaken = !nameMissing
            && _characterStorage.GetAllCharacters().Any(character => character.Name == currentName);
        bool canCreate = !nameMissing && !nameTaken;
        FooterStatus status = ResolveFooterStatus(nameMissing, nameTaken);

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
                _localization.GetRequired("ui.button.back"),
                _backButton.FooterButton(MenuLayout.RETURN_BUTTON_WIDTH)
            ))
            {
                menu.GoToPage(MenuPage.SelectCharacter);
            }

            ui.FooterStatus(status.Message, status.Color);

            Widgets.ActionButtonOptions createOptions = _createButton
                .FooterButton(MenuLayout.PRIMARY_BUTTON_WIDTH) with
            {
                Enabled = canCreate,
            };
            if (ui.ActionButton(
                "Button_CreateCharacter",
                _localization.GetRequired("ui.button.createCharacter"),
                createOptions
            ))
            {
                RequestCreate(menu);
            }
        }
    }

    /// <summary>Gets the footer message and its color for the current name and unspent spacer points.</summary>
    private FooterStatus ResolveFooterStatus(bool nameMissing, bool nameTaken)
    {
        if (nameMissing)
        {
            return new FooterStatus(_localization.GetString("ui.notification.name.required"), UiTheme.Danger);
        }

        if (nameTaken)
        {
            return new FooterStatus(_localization.GetString("ui.notification.name.taken"), UiTheme.Danger);
        }

        if (_spacerPoints == 0)
        {
            return default;
        }

        string message = string.Format(
            _localization.GetRequired("ui.menu.unspentPointsWarning"),
            _spacerPoints
        );

        return new FooterStatus(message, UiTheme.Warning);
    }

    /// <summary>A footer message and the color to draw it in.</summary>
    private readonly record struct FooterStatus(string? Message, Vector4 Color);

    /// <summary>Creates the character, or asks for confirmation first while spacer points are unspent.</summary>
    private void RequestCreate(Menu<MenuPage> menu)
    {
        if (_spacerPoints == 0)
        {
            TryCreate(menu);
            return;
        }

        string title = _localization.GetRequired("ui.menu.unspentPoints");
        string body = string.Format(
            _localization.GetRequired("ui.menu.unspentPointsBody"),
            _spacerPoints
        );
        string createLabel = _localization.GetRequired("ui.button.createCharacter");
        string cancelLabel = _localization.GetRequired("ui.button.cancel");
        _modalMenu.GoToPage(_confirmModal.Create(() => TryCreate(menu), title, body, createLabel, cancelLabel));
    }

    /// <summary>Draws the identity panel.</summary>
    private void DrawIdentity(UIBuilder<Material> ui, Constraints constraints)
    {
        using (ui.MenuPanel(constraints, _localization.GetRequired("ui.menu.colonistIdentity"), padding: PANEL_PADDING))
        {
            DrawNameField(ui);
            ui.Spacer(height: 24);
            DrawBodyPreview(ui);
        }
    }

    /// <summary>Draws the character name field and its randomize button.</summary>
    private void DrawNameField(UIBuilder<Material> ui)
    {
        int nameGroupWidth = INPUT_WIDTH + 8 + UiMetrics.SMALL_ICON_BUTTON_SIZE;

        ui.Label(
            _localization.GetRequired("ui.field.characterNameLabel"),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Text,
            new Constraints
            {
                Width = new Fixed(nameGroupWidth),
                Height = new Fixed(16),
                Anchors = Anchors.Center,
            }
        );
        ui.Spacer(height: 4);

        using (ui.Row(
            new Constraints
            {
                Width = new Fixed(nameGroupWidth),
                Height = new Fixed(UiMetrics.CONTROL_HEIGHT),
                Anchors = Anchors.Center,
            },
            spacing: 8
        ))
        {
            Widgets.Interactions nameInteractions = ui.MenuTextBox(
                "TextBox_CharacterName",
                ref _nameTextBox,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                },
                new FontOptions { Size = UiMetrics.MENU_BODY_FONT },
                _inputService,
                _soundEffectService
            );
            Widgets.IconButtonOptions randomOptions = new(SoundEffectService: _soundEffectService);
            if (ui.IconButton(
                "Button_RandomName",
                MenuGlyphs.DICE,
                randomOptions,
                new Constraints
                {
                    Width = new Fixed(UiMetrics.SMALL_ICON_BUTTON_SIZE),
                    Height = new Fixed(UiMetrics.SMALL_ICON_BUTTON_SIZE),
                    Anchors = Anchors.Center,
                }
            ) && !nameInteractions.Has(Widgets.Interactions.Input))
            {
                RandomizeName();
            }
        }
    }

    /// <summary>Draws the body preview box, its cycle buttons, and the preset labels.</summary>
    private void DrawBodyPreview(UIBuilder<Material> ui)
    {
        const int groupWidth = 32 + 8 + BODY_BOX_SIZE + 8 + 32 + 8 + 32;

        using (ui.Row(
            new Constraints
            {
                Width = new Fixed(groupWidth),
                Height = new Fixed(BODY_BOX_SIZE),
                Anchors = Anchors.Center,
            },
            spacing: 8
        ))
        {
            Widgets.IconButtonOptions previousOptions = new(SoundEffectService: _soundEffectService);
            if (ui.IconButton(
                "Button_PreviousCharacter",
                MenuGlyphs.CHEVRON_LEFT,
                previousOptions,
                new Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                    Anchors = Anchors.Center,
                }
            ))
            {
                CycleBody(-1);
            }

            DrawBodyImage(ui);

            Widgets.IconButtonOptions nextOptions = new(SoundEffectService: _soundEffectService);
            if (ui.IconButton(
                "Button_NextCharacter",
                MenuGlyphs.CHEVRON_RIGHT,
                nextOptions,
                new Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                    Anchors = Anchors.Center,
                }
            ))
            {
                CycleBody(1);
            }

            Widgets.IconButtonOptions randomOptions = new(SoundEffectService: _soundEffectService);
            if (ui.IconButton(
                "Button_RandomCharacter",
                MenuGlyphs.DICE,
                randomOptions,
                new Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                    Anchors = Anchors.Center,
                }
            ) && _characterAssetService.GetAppearancesCount() > 0)
            {
                _characterMaterialIndex = _randomizer.NextInt(0, _characterAssetService.GetAppearancesCount());
            }
        }

        ui.Spacer(height: 14);
        ui.Label(
            _localization.GetRequired("ui.field.bodyPreset"),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(18),
            },
            HorizontalAlignment.Center
        );
        ui.Label(
            FormatBodyId(_characterAssetService.GetBodyId(_characterMaterialIndex)),
            UiMetrics.MENU_BODY_FONT,
            UiTheme.AccentBright,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(24),
            },
            HorizontalAlignment.Center
        );
    }

    /// <summary>Draws the body preview frame and the current appearance inside it.</summary>
    private void DrawBodyImage(UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Padding = new Padding(8);
            ui.Color = UiTheme.PanelInner;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.Border,
            };
            ui.Constraints = new Constraints
            {
                Width = new Fixed(BODY_BOX_SIZE),
                Height = new Fixed(BODY_BOX_SIZE),
            };
            ui.ClipConstraints = Widgets.ClipToBounds();

            Material appearance = _characterAssetService.GetAppearanceMaterial(
                _characterAssetService.GetBodyId(_characterMaterialIndex)
            );
            using (ui.Image(appearance))
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }
        }
    }

    /// <summary>Draws the spacer evaluation panel.</summary>
    private void DrawEvaluation(UIBuilder<Material> ui, Constraints constraints)
    {
        using (ui.MenuPanel(constraints, _localization.GetRequired("ui.menu.spacerEvaluation"), padding: 16))
        {
            ui.Paragraph(
                _localization.GetRequired("ui.field.spacer.description"),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.TextMuted,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fit(),
                }
            );

            ui.Spacer(height: 12);

            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(32),
                },
                spacing: 8
            ))
            {
                ui.Label(
                    _localization.GetRequired("ui.field.spacer.points"),
                    UiMetrics.MENU_SMALL_FONT,
                    UiTheme.Text,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    }
                );
                ui.Label(
                    _spacerPoints.ToString(),
                    UiMetrics.MENU_BODY_FONT,
                    UiTheme.Primary,
                    new Constraints
                    {
                        Width = new Fixed(36),
                        Height = new Fill(),
                    },
                    HorizontalAlignment.Right
                );

                Widgets.IconButtonOptions randomOptions = new(SoundEffectService: _soundEffectService);
                if (ui.IconButton(
                    "Button_RandomPoints",
                    MenuGlyphs.DICE,
                    randomOptions,
                    new Constraints
                    {
                        Width = new Fixed(32),
                        Height = new Fixed(32),
                    }
                ))
                {
                    RandomizePoints();
                }
            }

            ui.Spacer(height: 8);
            DrawStatRows(ui);
            ui.Spacer(height: 12);

            string[] descriptions =
            [
                _localization.GetRequired("ui.field.strength.desc"),
                _localization.GetRequired("ui.field.precision.desc"),
                _localization.GetRequired("ui.field.awareness.desc"),
                _localization.GetRequired("ui.field.charisma.desc"),
                _localization.GetRequired("ui.field.education.desc"),
                _localization.GetRequired("ui.field.resolve.desc"),
            ];
            string[] names =
            [
                _localization.GetRequired("ui.field.strength"),
                _localization.GetRequired("ui.field.precision"),
                _localization.GetRequired("ui.field.awareness"),
                _localization.GetRequired("ui.field.charisma"),
                _localization.GetRequired("ui.field.education"),
                _localization.GetRequired("ui.field.resolve"),
            ];

            ui.InfoBox(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                    MaxHeight = 96,
                },
                _localization.GetRequired("ui.menu.info"),
                descriptions[_focusedStat],
                names[_focusedStat] + ":"
            );
        }
    }

    /// <summary>Draws the six spacer stat rows.</summary>
    private void DrawStatRows(UIBuilder<Material> ui)
    {
        DrawStatRow(ui, 0, _localization.GetRequired("ui.field.strength"), _strength);
        DrawStatRow(ui, 1, _localization.GetRequired("ui.field.precision"), _precision);
        DrawStatRow(ui, 2, _localization.GetRequired("ui.field.awareness"), _awareness);
        DrawStatRow(ui, 3, _localization.GetRequired("ui.field.charisma"), _charisma);
        DrawStatRow(ui, 4, _localization.GetRequired("ui.field.education"), _education);
        DrawStatRow(ui, 5, _localization.GetRequired("ui.field.resolve"), _resolve);
    }

    private void DrawStatRow(
        UIBuilder<Material> ui,
        int index,
        string label,
        int value
    )
    {
        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(28),
            },
            spacing: 4,
            id: $"StatRow_{index}"
        ))
        {
            bool focused = _focusedStat == index;
            bool hovering = ui.Hovering();
            if (hovering)
            {
                _focusedStat = index;
                focused = true;
            }

            ui.StatLabel(
                label,
                UiMetrics.MENU_BODY_FONT,
                focused,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            );
            ui.Label(
                value.ToString(),
                UiMetrics.MENU_BODY_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fixed(28),
                    Height = new Fill(),
                },
                HorizontalAlignment.Right
            );

            bool canDecrease = value > MIN_STAT;
            var buttonOptions = new Widgets.ActionButtonOptions(
                Variant: ButtonVariant.Secondary,
                Layout: new Constraints
                {
                    Width = new Fixed(24),
                    Height = new Fixed(24),
                    Anchors = Anchors.Center,
                }
            );
            if (ui.ActionButton(
                $"StatMinus_{index}",
                "-",
                buttonOptions with { Enabled = canDecrease }
            ))
            {
                _focusedStat = index;
                SetStat(index, value - 1);
                Widgets.Interactions.Click.WithButtonAudio(_soundEffectService, Widgets.InterfaceSound.Decrease);
            }

            bool canIncrease = _spacerPoints > 0 && value < MAX_STAT;
            if (ui.ActionButton(
                $"StatPlus_{index}",
                "+",
                buttonOptions with { Enabled = canIncrease }
            ))
            {
                _focusedStat = index;
                SetStat(index, value + 1);
                Widgets.Interactions.Click.WithButtonAudio(_soundEffectService, Widgets.InterfaceSound.Increase);
            }
        }
    }

    /// <summary>Gets the trimmed character name the field currently holds.</summary>
    private string CurrentName()
    {
        return CharacterNames.Clamp(_nameTextBox.Text.ToString().Trim(_characterNameTrimChars));
    }

    private void SetStat(int index, int value)
    {
        int oldValue = index switch
        {
            0 => _strength,
            1 => _precision,
            2 => _awareness,
            3 => _charisma,
            4 => _education,
            5 => _resolve,
            _ => 1,
        };
        int clamped = Math.Clamp(value, MIN_STAT, MAX_STAT);
        int change = clamped - oldValue;
        if (change == 0)
        {
            return;
        }

        if (change > 0 && _spacerPoints - change < 0)
        {
            return;
        }

        switch (index)
        {
            case 0:
                _strength = clamped;
                break;
            case 1:
                _precision = clamped;
                break;
            case 2:
                _awareness = clamped;
                break;
            case 3:
                _charisma = clamped;
                break;
            case 4:
                _education = clamped;
                break;
            case 5:
                _resolve = clamped;
                break;
        }

        _spacerPoints -= change;
    }

    private void TryCreate(Menu<MenuPage> menu)
    {
        string nameValue = CurrentName();
        if (string.IsNullOrWhiteSpace(nameValue))
        {
            _notificationService.Push(new Notification(_localization.GetRequired("ui.notification.name.required")));
            return;
        }

        if (_characterStorage.GetAllCharacters().Any(character => character.Name == nameValue))
        {
            _notificationService.Push(new Notification(_localization.GetRequired("ui.notification.name.taken")));
            return;
        }

        var character = new Character(
            WaywardBeyond.Version,
            Uuid.NewUuid().ToValue(),
            _Playtime: new Playtime(),
            nameValue,
            _strength,
            _precision,
            _awareness,
            _charisma,
            _education,
            _resolve,
            _Body: _characterAssetService.GetBodyId(_characterMaterialIndex),
            _ActiveInventorySlot: 0,
            _GameMode: GameMode.Creative,
            _Statistics: null,
            _Inventory: null
        );

        Task.Run(() => SaveCharacter(character, menu));
    }

    /// <summary>Saves the new character, then opens the character list.</summary>
    private void SaveCharacter(Character character, Menu<MenuPage> menu)
    {
        Result saveResult = _characterStorage.SaveCharacter(character);
        if (saveResult)
        {
            _characterSaveManager.ActiveSave = character;
            menu.GoToPage(MenuPage.SelectCharacter);
            ResetState();
            return;
        }

        _notificationService.Push(new Notification("Failed to create character!"));
        _logger.LogError(
            saveResult.Exception,
            "Failed to save character \"{name}\" ({id}): {message}",
            character.Name,
            character.Uuid,
            saveResult.Message
        );
    }

    private void CycleBody(int direction)
    {
        int count = _characterAssetService.GetAppearancesCount();
        if (count == 0)
        {
            return;
        }

        _characterMaterialIndex = MathS.WrapInt(_characterMaterialIndex + direction, 0, count - 1);
    }

    private void RandomizeName()
    {
        NameGenerator.Options options = new(
            TitleChance: 0.2f,
            FirstNameChance: 0.7f,
            LastNameChance: 1.0f,
            SubtitleChance: 0.1f,
            NicknameChance: 0.1f
        );

        string generatedName = _nameGenerator.Generate(
            key: _characterAssetService.GetBodyId(_characterMaterialIndex),
            options
        );
        _nameTextBox.Text.Clear();
        _nameTextBox.Text.Append(generatedName);
    }

    private void RandomizePoints()
    {
        if (_spacerPoints == 0)
        {
            ResetAttributes();
        }

        while (_spacerPoints > 0)
        {
            int[] candidates = Enumerable.Range(0, 6)
                .Where(index => index switch
                {
                    0 => _strength,
                    1 => _precision,
                    2 => _awareness,
                    3 => _charisma,
                    4 => _education,
                    5 => _resolve,
                    _ => MAX_STAT,
                } < MAX_STAT)
                .ToArray();

            if (candidates.Length == 0)
            {
                break;
            }

            int attributeIndex = candidates[_randomizer.NextInt(0, candidates.Length)];
            int value = attributeIndex switch
            {
                0 => _strength,
                1 => _precision,
                2 => _awareness,
                3 => _charisma,
                4 => _education,
                5 => _resolve,
                _ => MAX_STAT,
            };
            SetStat(attributeIndex, value + 1);
        }
    }

    private void ResetState()
    {
        _characterMaterialIndex = 0;
        _nameTextBox.Text.Clear();
        ResetAttributes();
    }

    private void ResetAttributes()
    {
        _spacerPoints = DEFAULT_SPACER_POINTS;
        _strength = 1;
        _precision = 1;
        _awareness = 1;
        _charisma = 1;
        _education = 1;
        _resolve = 1;
        _focusedStat = 0;
    }

    private static string FormatBodyId(string id)
    {
        string[] parts = id.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
