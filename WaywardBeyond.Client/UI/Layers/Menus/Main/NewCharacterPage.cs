using Microsoft.Extensions.Logging;
using Reef.Constraints;
using Reef.UI;
using Reef;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System;
using WaywardBeyond.Bodies;
using WaywardBeyond.Client.Extensions;
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
    private const int BODY_BOX_SIZE = 192;
    private const int FIELD_GROUP_WIDTH = INPUT_WIDTH + 8 + UiMetrics.SMALL_ICON_BUTTON_SIZE;
    private const int SELECTOR_LABEL_WIDTH = 76;

    private static readonly char[] _characterNameTrimChars = [' ', '\t', '.', '\n', '\r'];

    public MenuPage ID => MenuPage.NewCharacter;

    private readonly ILogger<NewCharacterPage> _logger;
    private readonly ICharacterStorage _characterStorage;
    private readonly CharacterSaveManager _characterSaveManager;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly IBodyDatabase _bodyDatabase;
    private readonly CharacterAssetService _characterAssetService;
    private readonly NameGenerator _nameGenerator;
    private readonly NotificationService _notificationService;
    private readonly ModalMenu _modalMenu;
    private readonly ConfirmModal _confirmModal;

    private readonly Widgets.ActionButtonOptions _backButton;
    private readonly Widgets.ActionButtonOptions _createButton;
    private readonly Randomizer _randomizer = new();

    private int _speciesIndex;
    private Gender _gender = Gender.Male;
    private Gender _rememberedGender = Gender.Male;
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
        in IBodyDatabase bodyDatabase,
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
        _bodyDatabase = bodyDatabase;
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
            ui.Spacer(height: 16);
            DrawBodyPreview(ui);
        }
    }

    /// <summary>Draws the character name field and its randomize button.</summary>
    private void DrawNameField(UIBuilder<Material> ui)
    {
        ui.Label(
            _localization.GetRequired("ui.field.characterNameLabel"),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Text,
            new Constraints
            {
                Width = new Fixed(FIELD_GROUP_WIDTH),
                Height = new Fixed(16),
                Anchors = Anchors.Center,
            }
        );
        ui.Spacer(height: 4);

        using (ui.Row(
            new Constraints
            {
                Width = new Fixed(FIELD_GROUP_WIDTH),
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

    /// <summary>Draws the body preview box and the species and gender selectors.</summary>
    private void DrawBodyPreview(UIBuilder<Material> ui)
    {
        DrawBodyImage(ui);
        ui.Spacer(height: 12);
        DrawBodySelectors(ui);
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
                Anchors = Anchors.Center,
            };
            ui.ClipConstraints = Widgets.ClipToBounds();

            Material appearance = _characterAssetService.GetAppearanceMaterial(CurrentBodyId());
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

    /// <summary>Draws the species and gender selector rows beneath the preview.</summary>
    private void DrawBodySelectors(UIBuilder<Material> ui)
    {
        EnsureValidSelection();

        IReadOnlyList<string> speciesList = _bodyDatabase.Species;
        if (speciesList.Count == 0)
        {
            return;
        }

        DrawSelectorRow(
            ui,
            "SpeciesSelector",
            _localization.GetRequired("ui.field.species"),
            speciesList,
            _speciesIndex,
            FormatSpeciesName,
            index =>
            {
                _speciesIndex = index;
                EnsureValidSelection();
            },
            RandomizeSpecies
        );

        ui.Spacer(height: 8);

        //  The species row may have changed the selection in this frame.
        IReadOnlyList<Gender> genders = _bodyDatabase.GetGenders(CurrentSpecies());
        DrawSelectorRow(
            ui,
            "GenderSelector",
            _localization.GetRequired("ui.field.gender"),
            genders,
            IndexOfGender(genders, _gender),
            FormatGenderName,
            index => SetGender(genders[index]),
            RandomizeGender
        );
    }

    /// <summary>Draws one labeled selector row with a discrete stepper and a randomize button.</summary>
    private void DrawSelectorRow<T>(
        UIBuilder<Material> ui,
        string id,
        string label,
        IReadOnlyList<T> options,
        int index,
        Func<T, string> format,
        Action<int> onChanged,
        Action onRandomize
    )
    {
        bool enabled = options.Count > 1;

        using (ui.Row(
            new Constraints
            {
                Width = new Fixed(FIELD_GROUP_WIDTH),
                Height = new Fixed(UiMetrics.CONTROL_HEIGHT),
                Anchors = Anchors.Center,
            },
            spacing: 8
        ))
        {
            ui.Label(
                label,
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.TextMuted,
                new Constraints
                {
                    Width = new Fixed(SELECTOR_LABEL_WIDTH),
                    Height = new Fill(),
                }
            );

            ControlResult<int> result = ui.Stepper(
                id,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                },
                options,
                index,
                format,
                enabled
            );

            if (result.Changed)
            {
                onChanged(result.Value);
                Widgets.Interactions.Click.WithButtonAudio(
                    _soundEffectService,
                    result.Value > index ? Widgets.InterfaceSound.Increase : Widgets.InterfaceSound.Decrease
                );
            }

            Widgets.IconButtonOptions randomOptions = new(
                SoundEffectService: _soundEffectService,
                Enabled: enabled
            );
            if (ui.IconButton(
                $"{id}_Random",
                MenuGlyphs.DICE,
                randomOptions,
                new Constraints
                {
                    Width = new Fixed(UiMetrics.SMALL_ICON_BUTTON_SIZE),
                    Height = new Fixed(UiMetrics.SMALL_ICON_BUTTON_SIZE),
                    Anchors = Anchors.Center,
                }
            ))
            {
                onRandomize();
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
            _Body: CurrentBodyId(),
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
            key: CurrentBodyId(),
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
        _speciesIndex = 0;
        _gender = Gender.Male;
        _rememberedGender = Gender.Male;
        EnsureValidSelection();
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

    /// <summary>Gets the body ID for the current species and gender.</summary>
    private string CurrentBodyId()
    {
        return _characterAssetService.GetBodyId(CurrentSpecies(), _gender);
    }

    /// <summary>Clamps the species index and keeps the gender valid for the current species.</summary>
    private void EnsureValidSelection()
    {
        IReadOnlyList<string> speciesList = _bodyDatabase.Species;
        if (speciesList.Count == 0)
        {
            _speciesIndex = 0;
            return;
        }

        _speciesIndex = Math.Clamp(_speciesIndex, 0, speciesList.Count - 1);
        IReadOnlyList<Gender> genders = _bodyDatabase.GetGenders(speciesList[_speciesIndex]);
        if (genders.Contains(_gender))
        {
            return;
        }

        if (genders.Contains(_rememberedGender))
        {
            _gender = _rememberedGender;
        }
        else
        {
            _gender = genders.Count > 0 ? genders[0] : Gender.Neutral;
        }
    }

    /// <summary>Selects a gender and remembers it while it is not neutral.</summary>
    private void SetGender(Gender gender)
    {
        _gender = gender;
        if (gender != Gender.Neutral)
        {
            _rememberedGender = gender;
        }
    }

    /// <summary>Picks a different species at random, then keeps the gender valid.</summary>
    private void RandomizeSpecies()
    {
        IReadOnlyList<string> speciesList = _bodyDatabase.Species;
        if (speciesList.Count <= 1)
        {
            return;
        }

        int nextIndex;
        do
        {
            nextIndex = _randomizer.NextInt(0, speciesList.Count);
        } while (nextIndex == _speciesIndex);

        _speciesIndex = nextIndex;
        EnsureValidSelection();
    }

    /// <summary>Picks a different gender for the current species at random.</summary>
    private void RandomizeGender()
    {
        IReadOnlyList<Gender> genders = _bodyDatabase.GetGenders(CurrentSpecies());
        if (genders.Count <= 1)
        {
            return;
        }

        int currentIndex = IndexOfGender(genders, _gender);
        int nextIndex;
        do
        {
            nextIndex = _randomizer.NextInt(0, genders.Count);
        } while (nextIndex == currentIndex);

        SetGender(genders[nextIndex]);
    }

    /// <summary>Gets the current species ID, or an empty string when no body is loaded.</summary>
    private string CurrentSpecies()
    {
        IReadOnlyList<string> speciesList = _bodyDatabase.Species;
        return speciesList.Count > _speciesIndex ? speciesList[_speciesIndex] : string.Empty;
    }

    /// <summary>Gets the localized species name, falling back to the raw species ID.</summary>
    private string FormatSpeciesName(string species)
    {
        string key = $"body.species.{species}";
        string name = _localization.GetString(key) ?? key;
        return name == key ? species : name;
    }

    /// <summary>Gets the localized gender name.</summary>
    private string FormatGenderName(Gender gender)
    {
        return gender switch
        {
            Gender.Male => _localization.GetRequired("body.gender.male"),
            Gender.Female => _localization.GetRequired("body.gender.female"),
            _ => _localization.GetRequired("body.gender.neutral"),
        };
    }

    /// <summary>Gets the position of a gender in a list, or 0 when it is absent.</summary>
    private static int IndexOfGender(IReadOnlyList<Gender> genders, Gender gender)
    {
        for (var i = 0; i < genders.Count; i++)
        {
            if (genders[i] == gender)
            {
                return i;
            }
        }

        return 0;
    }
}
