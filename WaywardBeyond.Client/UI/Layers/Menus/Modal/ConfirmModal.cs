using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

internal class ConfirmModal(in ILogger<ConfirmModal> logger, in SoundEffectService soundEffectService, in ILocalization localization)
    : IMenuPage<Modal>
{
    private static Modal Modal { get; } = new(id: "core.confirm");

    private readonly ILogger<ConfirmModal> _logger = logger;
    private readonly ILocalization _localization = localization;

    private readonly ConcurrentStack<ConfirmRequest> _requests = new();

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
        const int panelWidth = 520;
        const int panelHeight = 250;

        using (ui.ModalDialog(panelWidth, panelHeight))
        {
            int padding = 28;
            int contentWidth = panelWidth - padding * 2;
            ConfirmRequest request = CurrentRequest();

            ui.Label(
                request.Title,
                Widgets.Rect(padding, 22, contentWidth, 30),
                UiMetrics.MenuTitleFont,
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
                UiMetrics.MenuBodyFont,
                UiTheme.TextMuted
            );

            int buttonY = panelHeight - 68;
            IntRect cancelRect = Widgets.Rect(padding, buttonY, 180, UiMetrics.IconButtonSize);
            if (ui.ActionButton("Button_No", cancelRect, request.CancelLabel, _secondaryButton))
            {
                _requests.TryPop(out _);
                menu.GoBack();
            }

            IntRect confirmRect = Widgets.Rect(panelWidth - padding - 180, buttonY, 180, UiMetrics.IconButtonSize);
            if (ui.ActionButton("Button_Yes", confirmRect, request.ConfirmLabel, _primaryButton))
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
            title ?? _localization.GetString("ui.menu.confirm")!,
            body ?? _localization.GetString("ui.menu.confirmBody")!,
            confirmLabel ?? _localization.GetString("ui.button.yes")!,
            cancelLabel ?? _localization.GetString("ui.button.no")!
        ));
        return new Modal(id: "core.confirm");
    }

    private ConfirmRequest CurrentRequest()
    {
        return _requests.TryPeek(out ConfirmRequest request)
            ? request
            : new ConfirmRequest(
                static () => { },
                _localization.GetString("ui.menu.confirm")!,
                _localization.GetString("ui.menu.confirmBody")!,
                _localization.GetString("ui.button.yes")!,
                _localization.GetString("ui.button.no")!
            );
    }

    private readonly record struct ConfirmRequest(
        Action Action,
        string Title,
        string Body,
        string ConfirmLabel,
        string CancelLabel
    );
}
