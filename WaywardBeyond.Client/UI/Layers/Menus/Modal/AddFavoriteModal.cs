using System;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Services;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Collects a host and port for a new favorite.</summary>
internal sealed class AddFavoriteModal : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.addFavorite");

    private readonly SoundEffectService _soundEffectService;
    private readonly AddFavoriteView _view;
    private AddFavoriteState _state = CreateState();
    private Menu<Modal>? _menu;
    private Action<string, int>? _submit;

    public AddFavoriteModal(
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization
    ) {
        _soundEffectService = soundEffectService;
        _view = new AddFavoriteView(localization, inputService)
        {
            CancelRequested = Cancel,
            SubmitRequested = Submit
        };
    }

    public Modal ID => Modal;

    /// <inheritdoc/>
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        _menu = menu;
        _view.Render(ui, _state);

        if (_state.Host.Submitted || _state.Port.Submitted)
        {
            Submit();
        }

        return Result.FromSuccess();
    }

    /// <summary>Prepares the modal for one favorite submission.</summary>
    public Modal Create(Action<string, int> submit, int defaultPort)
    {
        _submit = submit;
        _state = CreateState();
        if (defaultPort > 0)
        {
            _state.Port.Text.Append(defaultPort);
        }

        return Modal;
    }

    private void Cancel()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        _menu?.GoBack();
    }

    private void Submit()
    {
        _state.Host.Submitted = false;
        _state.Port.Submitted = false;

        string host = _state.Host.Text.ToString().Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            _state.ErrorKey = "ui.message.server.connect.required";
            return;
        }

        if (!int.TryParse(_state.Port.Text.ToString().Trim(), out int port) || port < 1 || port > 65535)
        {
            _state.ErrorKey = "ui.message.server.connect.invalidPort";
            return;
        }

        _state.ErrorKey = null;
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        _submit?.Invoke(host, port);
        _menu?.GoBack();
    }

    private static AddFavoriteState CreateState()
    {
        return new AddFavoriteState(CreateHostTextBox(), CreatePortTextBox());
    }

    private static TextBoxState CreateHostTextBox()
    {
        return new TextBoxState(
            initialValue: string.Empty,
            new TextBoxState.Options(
                MaxCharacters: 253
            )
        );
    }

    private static TextBoxState CreatePortTextBox()
    {
        return new TextBoxState(
            initialValue: string.Empty,
            new TextBoxState.Options(
                MaxCharacters: 5,
                DisallowedCharacters: [' ', '\t', '\n', '\r']
            )
        );
    }
}
