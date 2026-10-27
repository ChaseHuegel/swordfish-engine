using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
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

    private TextBoxState _hostTextBox = CreateHostTextBox(localization.GetString("ui.field.host")!);
    private TextBoxState _portTextBox = CreatePortTextBox(localization.GetString("ui.field.port")!);
    private Action<string, int>? _submit;
    private string? _errorMessage;

    public Modal ID => Modal;

    /// <inheritdoc/>
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        const int panelWidth = 520;
        const int panelHeight = 300;

        using (ui.ModalDialog(panelWidth, panelHeight))
        {
            int padding = 28;
            int contentWidth = panelWidth - padding * 2;

            ui.Label(
                _localization.GetString("ui.menu.addFavorite")!,
                Widgets.Rect(padding, 22, contentWidth, 30),
                UiMetrics.MenuTitleFont,
                UiTheme.Text,
                Anchors.Center
            );
            ui.Divider(Widgets.Rect(padding, 62, contentWidth, 1), UiTheme.Border);

            ui.Label(
                _localization.GetString("ui.field.host")!,
                Widgets.Rect(padding, 78, contentWidth, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
            using (ui.Element())
            {
                ui.Constraints = Widgets.RectConstraints(
                    Widgets.Rect(padding, 98, contentWidth, UiMetrics.ControlHeight)
                );
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(contentWidth),
                    Height = new Fixed(UiMetrics.ControlHeight),
                };
                ui.TextBox(
                    "TextBox_FavoriteHost",
                    ref _hostTextBox,
                    new FontOptions { Size = UiMetrics.MenuBodyFont },
                    _inputService,
                    out Widgets.Interactions hostInteractions
                );
                if (hostInteractions.Has(Widgets.Interactions.Input))
                {
                    _errorMessage = null;
                }

                if (hostInteractions.Has(Widgets.Interactions.Submit))
                {
                    Submit(menu);
                }
            }

            ui.Label(
                _localization.GetString("ui.field.port")!,
                Widgets.Rect(padding, 142, contentWidth, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
            using (ui.Element())
            {
                ui.Constraints = Widgets.RectConstraints(
                    Widgets.Rect(padding, 162, 96, UiMetrics.ControlHeight)
                );
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(96),
                    Height = new Fixed(UiMetrics.ControlHeight),
                };
                ui.TextBox(
                    "TextBox_FavoritePort",
                    ref _portTextBox,
                    new FontOptions { Size = UiMetrics.MenuBodyFont },
                    _inputService,
                    out Widgets.Interactions portInteractions
                );
                if (portInteractions.Has(Widgets.Interactions.Input))
                {
                    _errorMessage = null;
                }

                if (portInteractions.Has(Widgets.Interactions.Submit))
                {
                    Submit(menu);
                }
            }

            if (_errorMessage != null)
            {
                ui.Label(
                    _errorMessage,
                    Widgets.Rect(padding, 204, contentWidth, 18),
                    UiMetrics.MenuSmallFont,
                    UiTheme.Danger,
                    Anchors.Left | Anchors.Center
                );
            }

            int buttonY = panelHeight - 68;
            IntRect cancelRect = Widgets.Rect(padding, buttonY, 180, UiMetrics.IconButtonSize);
            string cancelLabel = _localization.GetString("ui.button.cancel")!;
            if (ui.ActionButton("Button_CancelFavorite", cancelRect, cancelLabel, _cancelButton))
            {
                menu.GoBack();
            }

            IntRect submitRect = Widgets.Rect(panelWidth - padding - 180, buttonY, 180, UiMetrics.IconButtonSize);
            string submitLabel = _localization.GetString("ui.button.submit")!;
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

        return new Modal(id: "core.addFavorite");
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
