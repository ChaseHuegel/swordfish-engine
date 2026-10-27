using Reef.UI;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Shows one message with a single Ok action.</summary>
internal sealed class MessageModal(
    in SoundEffectService soundEffectService,
    in ILocalization localization
) : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.message");

    /// <summary>The panel width in pixels.</summary>
    private const int PANEL_WIDTH = 520;

    /// <summary>The panel height in pixels.</summary>
    private const int PANEL_HEIGHT = 250;

    private readonly ILocalization _localization = localization;
    private readonly Widgets.ActionButtonOptions _okButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Primary,
        soundEffectService
    );

    private string _title = string.Empty;
    private string _body = string.Empty;

    public Modal ID => Modal;

    /// <inheritdoc/>
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        ui.ModalFrame(PANEL_WIDTH, PANEL_HEIGHT, frame => DrawBody(frame, menu));

        return Result.FromSuccess();
    }

    /// <summary>Prepares the modal for one message.</summary>
    public Modal Create(string title, string body)
    {
        _title = title;
        _body = body;
        return new Modal(id: "core.message");
    }

    /// <summary>Draws the title, body, and Ok button inside the modal panel.</summary>
    private void DrawBody(UIBuilder<Material> ui, Menu<Modal> menu)
    {
        int padding = UiMetrics.MODAL_PADDING;
        int contentWidth = PANEL_WIDTH - padding * 2;

        ui.Label(
            _title,
            Widgets.Rect(padding, 22, contentWidth, 30),
            UiMetrics.MENU_TITLE_FONT,
            UiTheme.Text,
            Anchors.Center
        );
        ui.Divider(Widgets.Rect(padding, 62, contentWidth, 1), UiTheme.Border);
        ui.Paragraph(
            _body,
            Widgets.Rect(padding, 86, contentWidth, 56),
            UiMetrics.MENU_BODY_FONT,
            UiTheme.TextMuted
        );

        Widgets.ActionButtonOptions okOptions = _okButton with
        {
            Rect = Widgets.Rect(
                PANEL_WIDTH - padding - UiMetrics.MODAL_BUTTON_WIDTH,
                PANEL_HEIGHT - 68,
                UiMetrics.MODAL_BUTTON_WIDTH,
                UiMetrics.ICON_BUTTON_SIZE
            ),
        };

        if (ui.ActionButton("Button_Ok", _localization.GetRequired("ui.button.ok"), okOptions))
        {
            menu.GoBack();
        }
    }
}
