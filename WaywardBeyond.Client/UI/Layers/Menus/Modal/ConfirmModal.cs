using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Reef.Constraints;
using Reef.UI;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Asks the player to confirm one destructive action.</summary>
internal class ConfirmModal(
    in ILogger<ConfirmModal> logger,
    in SoundEffectService soundEffectService,
    in ILocalization localization
) : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.confirm");

    /// <summary>The panel width in pixels.</summary>
    private const int PANEL_WIDTH = 520;

    /// <summary>The panel height in pixels.</summary>
    private const int PANEL_HEIGHT = 250;

    private readonly ILogger<ConfirmModal> _logger = logger;
    private readonly ILocalization _localization = localization;
    private readonly Stack<ConfirmRequest> _requests = new();

    private readonly Widgets.ActionButtonOptions _secondaryButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Secondary,
        soundEffectService
    );
    private readonly Widgets.ActionButtonOptions _primaryButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Danger,
        soundEffectService
    );

    public Modal ID => Modal;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        ConfirmRequest request = CurrentRequest();

        using (ui.ModalDialog(PANEL_WIDTH, PANEL_HEIGHT))
        {
            ui.Label(
                request.Title,
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
            ui.Paragraph(
                request.Body,
                UiMetrics.MENU_BODY_FONT,
                UiTheme.TextMuted,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            );

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
                    "Button_No",
                    request.CancelLabel,
                    _secondaryButton with
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
                    _requests.TryPop(out _);
                    menu.GoBack();
                }

                ui.Filler();

                if (ui.ActionButton(
                    "Button_Yes",
                    request.ConfirmLabel,
                    _primaryButton with
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
                    Confirm(menu);
                }
            }
        }

        return Result.FromSuccess();
    }

    public Modal Create(
        Action action,
        string? title = null,
        string? body = null,
        string? confirmLabel = null,
        string? cancelLabel = null
    )
    {
        _requests.Clear();
        _requests.Push(new ConfirmRequest(
            action,
            title ?? _localization.GetRequired("ui.dialog.confirm"),
            body ?? _localization.GetRequired("ui.dialog.confirm.body"),
            confirmLabel ?? _localization.GetRequired("ui.button.confirm"),
            cancelLabel ?? _localization.GetRequired("ui.button.cancel")
        ));
        return Modal;
    }

    /// <summary>Runs the pending confirm action and closes the modal.</summary>
    private void Confirm(Menu<Modal> menu)
    {
        if (_requests.TryPop(out ConfirmRequest confirmed))
        {
            try
            {
                confirmed.Action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising a confirm modal action.");
            }
        }

        menu.GoBack();
    }

    private ConfirmRequest CurrentRequest()
    {
        return _requests.TryPeek(out ConfirmRequest request)
            ? request
            : new ConfirmRequest(
                static () => { },
                _localization.GetRequired("ui.dialog.confirm"),
                _localization.GetRequired("ui.dialog.confirm.body"),
                _localization.GetRequired("ui.button.confirm"),
                _localization.GetRequired("ui.button.cancel")
            );
    }

    /// <summary>One pending confirmation, with its labels already resolved.</summary>
    private readonly record struct ConfirmRequest(
        Action Action,
        string Title,
        string Body,
        string ConfirmLabel,
        string CancelLabel
    );
}
