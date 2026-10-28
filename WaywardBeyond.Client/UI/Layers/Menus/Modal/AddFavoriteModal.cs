using Reef.Constraints;
using Reef.UI;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using System;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Collects a host and port for a new favorite.</summary>
internal sealed class AddFavoriteModal(
    in IInputService inputService,
    in SoundEffectService soundEffectService,
    in ILocalization localization
) : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.addFavorite");

    /// <summary>The panel width in pixels.</summary>
    private const int PANEL_WIDTH = 520;

    /// <summary>The panel height in pixels.</summary>
    private const int PANEL_HEIGHT = 300;

    private readonly IInputService _inputService = inputService;
    private readonly ILocalization _localization = localization;

    private readonly Widgets.ActionButtonOptions _cancelButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Secondary,
        soundEffectService
    );
    private readonly Widgets.ActionButtonOptions _submitButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Primary,
        soundEffectService
    );

    private TextBoxState _hostTextBox = CreateHostTextBox(localization.GetRequired("ui.field.server.host"));
    private TextBoxState _portTextBox = CreatePortTextBox(localization.GetRequired("ui.field.server.port"));
    private Action<string, int>? _submit;
    private string? _errorMessage;

    public Modal ID => Modal;

    /// <inheritdoc/>
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        using (ui.ModalDialog(PANEL_WIDTH, PANEL_HEIGHT))
        {
            ui.Label(
                _localization.GetRequired("ui.dialog.server.add"),
                UiMetrics.MENU_TITLE_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(30),
                },
                HorizontalAlignment.Center
            );
            ui.Spacer(height: 8);
            ui.Divider(
                UiTheme.Border,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(1),
                }
            );
            ui.Spacer(height: 16);

            ui.Label(
                _localization.GetRequired("ui.field.server.host"),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(18),
                }
            );
            ui.Spacer(height: 4);
            Widgets.Interactions hostInteractions = ui.MenuTextBox(
                "TextBox_FavoriteHost",
                ref _hostTextBox,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(UiMetrics.CONTROL_HEIGHT),
                },
                new FontOptions { Size = UiMetrics.MENU_BODY_FONT },
                _inputService
            );
            HandleTextBoxInteractions(hostInteractions, menu);

            ui.Spacer(height: 12);
            ui.Label(
                _localization.GetRequired("ui.field.server.port"),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(18),
                }
            );
            ui.Spacer(height: 4);
            Widgets.Interactions portInteractions = ui.MenuTextBox(
                "TextBox_FavoritePort",
                ref _portTextBox,
                new Constraints
                {
                    Width = new Fixed(96),
                    Height = new Fixed(UiMetrics.CONTROL_HEIGHT),
                },
                new FontOptions { Size = UiMetrics.MENU_BODY_FONT },
                _inputService
            );
            HandleTextBoxInteractions(portInteractions, menu);

            if (_errorMessage != null)
            {
                ui.Spacer(height: 8);
                ui.Label(
                    _errorMessage,
                    UiMetrics.MENU_SMALL_FONT,
                    UiTheme.Danger,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fixed(18),
                    }
                );
            }

            ui.Filler();

            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
                },
                spacing: 12
            ))
            {
                if (ui.ActionButton(
                    "Button_CancelFavorite",
                    _localization.GetRequired("ui.button.cancel"),
                    _cancelButton with
                    {
                        Layout = new Constraints
                        {
                            Width = new Fixed(UiMetrics.MODAL_BUTTON_WIDTH),
                            Height = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
                            Anchors = Anchors.Center,
                        },
                    }
                ))
                {
                    menu.GoBack();
                }

                ui.Filler();

                if (ui.ActionButton(
                    "Button_SubmitFavorite",
                    _localization.GetRequired("ui.button.server.add"),
                    _submitButton with
                    {
                        Layout = new Constraints
                        {
                            Width = new Fixed(UiMetrics.MODAL_BUTTON_WIDTH),
                            Height = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
                            Anchors = Anchors.Center,
                        },
                    }
                ))
                {
                    Submit(menu);
                }
            }
        }

        return Result.FromSuccess();
    }

    /// <summary>Prepares the modal for one favorite submission.</summary>
    public Modal Create(Action<string, int> submit, int defaultPort)
    {
        _submit = submit;
        _errorMessage = null;
        _hostTextBox.Text.Clear();
        _hostTextBox.CaretIndex = 0;
        _hostTextBox.SelectionStartIndex = 0;
        _hostTextBox.ShiftModifier = false;
        _hostTextBox.ControlModifier = false;
        _portTextBox.Text.Clear();
        _portTextBox.CaretIndex = 0;
        _portTextBox.SelectionStartIndex = 0;
        _portTextBox.ShiftModifier = false;
        _portTextBox.ControlModifier = false;
        if (defaultPort > 0)
        {
            _portTextBox.Text.Append(defaultPort);
        }

        return Modal;
    }

    /// <summary>Applies the text box interactions shared by the host and port fields.</summary>
    private void HandleTextBoxInteractions(in Widgets.Interactions interactions, Menu<Modal> menu)
    {
        if (interactions.Has(Widgets.Interactions.Input))
        {
            _errorMessage = null;
        }

        if (interactions.Has(Widgets.Interactions.Submit))
        {
            Submit(menu);
        }
    }

    private void Submit(Menu<Modal> menu)
    {
        string host = _hostTextBox.Text.ToString().Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            _errorMessage = _localization.GetString("ui.message.server.connect.required");
            return;
        }

        if (!int.TryParse(_portTextBox.Text.ToString().Trim(), out int port) || port < 1 || port > 65535)
        {
            _errorMessage = _localization.GetString("ui.message.server.connect.invalidPort");
            return;
        }

        _errorMessage = null;
        _submit?.Invoke(host, port);
        menu.GoBack();
    }

    private static TextBoxState CreateHostTextBox(string placeholder)
    {
        return new TextBoxState(
            initialValue: string.Empty,
            new TextBoxState.Options(
                Placeholder: placeholder,
                MaxCharacters: 253,
                Constraints: new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Relative(1f),
                }
            )
        );
    }

    private static TextBoxState CreatePortTextBox(string placeholder)
    {
        return new TextBoxState(
            initialValue: string.Empty,
            new TextBoxState.Options(
                Placeholder: placeholder,
                MaxCharacters: 5,
                DisallowedCharacters: [' ', '\t', '\n', '\r'],
                Constraints: new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Relative(1f),
                }
            )
        );
    }
}
