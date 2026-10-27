using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
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
        const int panelWidth = 520;
        const int panelHeight = 250;

        using (ui.ModalDialog(panelWidth, panelHeight))
        {
            int padding = 28;
            int contentWidth = panelWidth - padding * 2;

            ui.Label(
                _title,
                Widgets.Rect(padding, 22, contentWidth, 30),
                UiMetrics.MenuTitleFont,
                UiTheme.Text,
                Anchors.Center
            );
            ui.Divider(Widgets.Rect(padding, 62, contentWidth, 1), UiTheme.Border);
            ui.Paragraph(
                _body,
                Widgets.Rect(padding, 86, contentWidth, 56),
                UiMetrics.MenuBodyFont,
                UiTheme.TextMuted
            );

            IntRect okRect = Widgets.Rect(
                panelWidth - padding - 180,
                panelHeight - 68,
                180,
                UiMetrics.IconButtonSize
            );
            string okLabel = _localization.GetString("ui.button.ok")!;
            if (ui.ActionButton("Button_Ok", okRect, okLabel, _okButton))
            {
                menu.GoBack();
            }
        }

        return Result.FromSuccess();
    }

    /// <summary>Prepares the modal for one message.</summary>
    public Modal Create(string title, string body)
    {
        _title = title;
        _body = body;
        return new Modal(id: "core.message");
    }
}
