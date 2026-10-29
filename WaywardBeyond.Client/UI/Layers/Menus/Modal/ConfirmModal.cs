using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Services;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Asks the player to confirm one destructive action.</summary>
internal sealed class ConfirmModal : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.confirm");

    private readonly ILogger<ConfirmModal> _logger;
    private readonly ILocalization _localization;
    private readonly SoundEffectService _soundEffectService;
    private readonly ConfirmModalView _view;
    private readonly Stack<ConfirmRequest> _requests = new();
    private Menu<Modal>? _menu;

    public ConfirmModal(
        in ILogger<ConfirmModal> logger,
        in SoundEffectService soundEffectService,
        in ILocalization localization
    ) {
        _logger = logger;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _view = new ConfirmModalView(localization)
        {
            ConfirmRequested = Confirm,
            CancelRequested = Cancel
        };
    }

    public Modal ID => Modal;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        _menu = menu;
        ConfirmRequest request = CurrentRequest();
        _view.Render(ui, new ConfirmModalState(
            request.Title,
            request.Body,
            request.ConfirmLabel,
            request.CancelLabel
        ));
        
        return Result.FromSuccess();
    }

    public Modal Create(
        Action action,
        string? title = null,
        string? body = null,
        string? confirmLabel = null,
        string? cancelLabel = null
    ) {
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

    private void Confirm()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        if (_requests.TryPop(out ConfirmRequest confirmed))
        {
            try
            {
                confirmed.Action();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error raising a confirm modal action.");
            }
        }

        _menu?.GoBack();
    }

    private void Cancel()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        _requests.TryPop(out _);
        _menu?.GoBack();
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

    private readonly record struct ConfirmRequest(
        Action Action,
        string Title,
        string Body,
        string ConfirmLabel,
        string CancelLabel
    );
}
