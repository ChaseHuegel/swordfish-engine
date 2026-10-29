using System;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Services;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Displays an input-blocking modal with an animated signal scanner during connection attempts.</summary>
internal sealed class ConnectingModal : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.connecting");

    private readonly ILocalization _localization;
    private readonly SoundEffectService _soundEffectService;
    private readonly ConnectingModalView _view;
    private Menu<Modal>? _menu;
    private string? _title;
    private string? _target;
    private Action? _onCancel;

    public Modal ID => Modal;

    public ConnectingModal(
        in SoundEffectService soundEffectService,
        in ILocalization localization
    )
    {
        _soundEffectService = soundEffectService;
        _localization = localization;
        _view = new ConnectingModalView(localization)
        {
            CancelRequested = Cancel
        };
    }

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
        _menu = menu;
        string title = _title ?? _localization.GetRequired("ui.dialog.server.connecting");
        string target = _target ?? _localization.GetRequired("ui.message.server.connecting");
        _view.Render(ui, new ConnectingModalState(title, target));
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

    private void Cancel()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        Action? cancel = _onCancel;
        _onCancel = null;
        cancel?.Invoke();
        _menu?.GoBack();
    }
}
