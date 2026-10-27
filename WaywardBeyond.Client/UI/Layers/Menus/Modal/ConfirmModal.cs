using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
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
        int padding = UiMetrics.MODAL_PADDING;
        int contentWidth = PANEL_WIDTH - padding * 2;
        ConfirmRequest request = CurrentRequest();

        using (ui.ModalDialog(PANEL_WIDTH, PANEL_HEIGHT))
        {
            ui.Label(
                request.Title,
                Widgets.Rect(padding, 22, contentWidth, 30),
                UiMetrics.MENU_TITLE_FONT,
                UiTheme.Text,
                Anchors.Center
            );
            ui.Divider(
                Widgets.Rect(padding, 62, contentWidth, 1),
                UiTheme.Border
            );
            ui.Paragraph(
                request.Body,
                Widgets.Rect(padding, 86, contentWidth, 56),
                UiMetrics.MENU_BODY_FONT,
                UiTheme.TextMuted
            );

            int buttonY = PANEL_HEIGHT - 68;
            IntRect cancelRect = Widgets.Rect(
                padding,
                buttonY,
                UiMetrics.MODAL_BUTTON_WIDTH,
                UiMetrics.ICON_BUTTON_SIZE
            );
            if (ui.ActionButton("Button_No", cancelRect, request.CancelLabel, _secondaryButton))
            {
                _requests.TryPop(out _);
                menu.GoBack();
            }

            IntRect confirmRect = Widgets.Rect(
                PANEL_WIDTH - padding - UiMetrics.MODAL_BUTTON_WIDTH,
                buttonY,
                UiMetrics.MODAL_BUTTON_WIDTH,
                UiMetrics.ICON_BUTTON_SIZE
            );
            if (ui.ActionButton("Button_Yes", confirmRect, request.ConfirmLabel, _primaryButton))
            {
                Confirm(menu);
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
            title ?? _localization.GetRequired("ui.menu.confirm"),
            body ?? _localization.GetRequired("ui.menu.confirmBody"),
            confirmLabel ?? _localization.GetRequired("ui.button.yes"),
            cancelLabel ?? _localization.GetRequired("ui.button.no")
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
                _localization.GetRequired("ui.menu.confirm"),
                _localization.GetRequired("ui.menu.confirmBody"),
                _localization.GetRequired("ui.button.yes"),
                _localization.GetRequired("ui.button.no")
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
