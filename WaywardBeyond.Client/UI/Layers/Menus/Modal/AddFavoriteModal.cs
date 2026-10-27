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

    private TextBoxState _hostTextBox = CreateHostTextBox(localization.GetRequired("ui.field.host"));
    private TextBoxState _portTextBox = CreatePortTextBox(localization.GetRequired("ui.field.port"));
    private Action<string, int>? _submit;
    private string? _errorMessage;

    public Modal ID => Modal;

    /// <inheritdoc/>
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        int padding = UiMetrics.MODAL_PADDING;
        int contentWidth = PANEL_WIDTH - padding * 2;

        using (ui.ModalDialog(PANEL_WIDTH, PANEL_HEIGHT))
        {
            ui.Label(
                _localization.GetRequired("ui.menu.addFavorite"),
                Widgets.Rect(padding, 22, contentWidth, 30),
                UiMetrics.MENU_TITLE_FONT,
                UiTheme.Text,
                Anchors.Center
            );
            ui.Divider(Widgets.Rect(padding, 62, contentWidth, 1), UiTheme.Border);

            ui.Label(
                _localization.GetRequired("ui.field.host"),
                Widgets.Rect(padding, 78, contentWidth, 18),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
            Widgets.Interactions hostInteractions = ui.MenuTextBox(
                "TextBox_FavoriteHost",
                ref _hostTextBox,
                Widgets.Rect(padding, 98, contentWidth, UiMetrics.CONTROL_HEIGHT),
                new FontOptions { Size = UiMetrics.MENU_BODY_FONT },
                _inputService
            );
            HandleTextBoxInteractions(hostInteractions, menu);

            ui.Label(
                _localization.GetRequired("ui.field.port"),
                Widgets.Rect(padding, 142, contentWidth, 18),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
            Widgets.Interactions portInteractions = ui.MenuTextBox(
                "TextBox_FavoritePort",
                ref _portTextBox,
                Widgets.Rect(padding, 162, 96, UiMetrics.CONTROL_HEIGHT),
                new FontOptions { Size = UiMetrics.MENU_BODY_FONT },
                _inputService
            );
            HandleTextBoxInteractions(portInteractions, menu);

            if (_errorMessage != null)
            {
                ui.Label(
                    _errorMessage,
                    Widgets.Rect(padding, 204, contentWidth, 18),
                    UiMetrics.MENU_SMALL_FONT,
                    UiTheme.Danger,
                    Anchors.Left | Anchors.Center
                );
            }

            int buttonY = PANEL_HEIGHT - 68;
            IntRect cancelRect = Widgets.Rect(
                padding,
                buttonY,
                UiMetrics.MODAL_BUTTON_WIDTH,
                UiMetrics.ICON_BUTTON_SIZE
            );
            string cancelLabel = _localization.GetRequired("ui.button.cancel");
            if (ui.ActionButton("Button_CancelFavorite", cancelRect, cancelLabel, _cancelButton))
            {
                menu.GoBack();
            }

            IntRect submitRect = Widgets.Rect(
                PANEL_WIDTH - padding - UiMetrics.MODAL_BUTTON_WIDTH,
                buttonY,
                UiMetrics.MODAL_BUTTON_WIDTH,
                UiMetrics.ICON_BUTTON_SIZE
            );
            string submitLabel = _localization.GetRequired("ui.button.submit");
            if (ui.ActionButton("Button_SubmitFavorite", submitRect, submitLabel, _submitButton))
            {
                Submit(menu);
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
            _errorMessage = _localization.GetString("ui.notification.connect.hostRequired");
            return;
        }

        if (!int.TryParse(_portTextBox.Text.ToString().Trim(), out int port) || port < 1 || port > 65535)
        {
            _errorMessage = _localization.GetString("ui.notification.connect.portInvalid");
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
