using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Services;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Shows one message with a single Ok action.</summary>
internal sealed class MessageModal : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.message");

    private readonly SoundEffectService _soundEffectService;
    private readonly MessageModalView _view;
    private Menu<Modal>? _menu;
    private string _title = string.Empty;
    private string _body = string.Empty;

    public MessageModal(in SoundEffectService soundEffectService, in ILocalization localization)
    {
        _soundEffectService = soundEffectService;
        _view = new MessageModalView(localization);
        _view.CloseRequested = Close;
    }

    public Modal ID => Modal;

    /// <inheritdoc/>
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        _menu = menu;
        _view.Render(ui, new MessageModalState(_title, _body));
        return Result.FromSuccess();
    }

    /// <summary>Prepares the modal for one message.</summary>
    public Modal Create(string title, string body)
    {
        _title = title;
        _body = body;
        return Modal;
    }

    private void Close()
    {
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        _menu?.GoBack();
    }
}
