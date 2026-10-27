using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Displays an input-blocking modal with an animated signal scanner during connection attempts.</summary>
internal sealed class ConnectingModal(
    in SoundEffectService soundEffectService,
    in ILocalization localization
) : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.connecting");

    private const int SIGNAL_WIDTH = 280;
    private const int SIGNAL_HEIGHT = 2;
    private const int SIGNAL_SEGMENT_WIDTH = 56;
    private const int SIGNAL_TRAVEL = SIGNAL_WIDTH + SIGNAL_SEGMENT_WIDTH * 2;
    private const float SIGNAL_PERIOD_SECONDS = 1.8f;

    private readonly ILocalization _localization = localization;
    private readonly Widgets.ActionButtonOptions _cancelButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Secondary,
        soundEffectService
    );

    private string? _title;
    private string? _target;
    private Action? _onCancel;

    public Modal ID => Modal;

    /// <inheritdoc/>
    public void OnExit()
    {
        Action? cancel = _onCancel;
        _onCancel = null;
        cancel?.Invoke();
    }

    /// <inheritdoc/>
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        const int panelWidth = 520;
        const int panelHeight = 240;

        using (ui.ModalDialog(panelWidth, panelHeight))
        {
            int padding = 28;
            int contentWidth = panelWidth - padding * 2;

            ui.Label(
                _title ?? _localization.GetString("ui.menu.connecting") ?? "Connecting to Server",
                Widgets.Rect(padding, 22, contentWidth, 30),
                UiMetrics.MenuTitleFont,
                UiTheme.Text,
                Anchors.Center
            );

            ui.Divider(Widgets.Rect(padding, 62, contentWidth, 1), UiTheme.Border);

            ui.Label(
                _target ?? _localization.GetString("ui.menu.connectingStatus") ?? "Establishing uplink with remote host...",
                Widgets.Rect(padding, 80, contentWidth, 22),
                UiMetrics.MenuBodyFont,
                UiTheme.TextMuted,
                Anchors.Center
            );

            int signalX = (panelWidth - SIGNAL_WIDTH) / 2;
            int signalY = 120;
            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Color = UiTheme.Border;
                ui.Constraints = Widgets.RectConstraints(
                    Widgets.Rect(signalX, signalY, SIGNAL_WIDTH, SIGNAL_HEIGHT)
                );
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(SIGNAL_WIDTH),
                    Height = new Fixed(SIGNAL_HEIGHT),
                };

                float progress = (float)(ui.Time / SIGNAL_PERIOD_SECONDS % 1d);
                int segmentX = (int)(progress * SIGNAL_TRAVEL) - SIGNAL_SEGMENT_WIDTH;

                using (ui.Element())
                {
                    ui.Color = UiTheme.Accent;
                    ui.Constraints = new Constraints
                    {
                        X = new Fixed(segmentX),
                        Y = new Fixed(0),
                        Width = new Fixed(SIGNAL_SEGMENT_WIDTH),
                        Height = new Fixed(SIGNAL_HEIGHT),
                    };
                }
            }

            int buttonWidth = 180;
            int buttonX = (panelWidth - buttonWidth) / 2;
            int buttonY = panelHeight - 68;
            IntRect buttonRect = Widgets.Rect(buttonX, buttonY, buttonWidth, UiMetrics.IconButtonSize);
            string buttonLabel = _localization.GetString("ui.button.cancel") ?? "Cancel";

            if (ui.ActionButton("Button_CancelConnect", buttonRect, buttonLabel, _cancelButton))
            {
                Cancel(menu);
            }
        }

        return Result.FromSuccess();
    }

    /// <summary>Prepares the modal for an active connection attempt.</summary>
    public Modal Create(Action? onCancel = null, string? title = null, string? target = null)
    {
        _onCancel = onCancel;
        _title = title;
        _target = target;
        return Modal;
    }

    /// <summary>Dismisses the modal programmatically without invoking cancellation.</summary>
    public void Close(Menu<Modal>? menu)
    {
        _onCancel = null;
        if (menu?.GetCurrentPage() == Modal)
        {
            menu.GoBack();
        }
    }

    private void Cancel(Menu<Modal> menu)
    {
        Action? cancel = _onCancel;
        _onCancel = null;
        cancel?.Invoke();
        menu.GoBack();
    }
}
