using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
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

    /// <summary>The panel width in pixels.</summary>
    private const int PANEL_WIDTH = 520;

    /// <summary>The panel height in pixels.</summary>
    private const int PANEL_HEIGHT = 240;

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
        using (ui.ModalDialog(PANEL_WIDTH, PANEL_HEIGHT))
        {
            ui.Label(
                _title ?? _localization.GetRequired("ui.dialog.server.connecting"),
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
                _target ?? _localization.GetRequired("ui.message.server.connecting"),
                UiMetrics.MENU_BODY_FONT,
                UiTheme.TextMuted,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(22),
                },
                HorizontalAlignment.Center
            );

            ui.Spacer(height: 16);
            DrawSignal(ui);
            ui.Filler();

            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(UiMetrics.ICON_BUTTON_SIZE),
                }
            ))
            {
                ui.Filler();
                string cancelLabel = _localization.GetRequired("ui.button.cancel");
                if (ui.ActionButton(
                    "Button_CancelConnect",
                    cancelLabel,
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
                    Cancel(menu);
                }
                ui.Filler();
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

    /// <summary>Draws the animated signal segment sweeping across the scanner track.</summary>
    private static void DrawSignal(UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.Border;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(SIGNAL_WIDTH),
                Height = new Fixed(SIGNAL_HEIGHT),
            };
            ui.ClipConstraints = Widgets.ClipToBounds();

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
    }

    private void Cancel(Menu<Modal> menu)
    {
        Action? cancel = _onCancel;
        _onCancel = null;
        cancel?.Invoke();
        menu.GoBack();
    }
}
