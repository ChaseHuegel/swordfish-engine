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
        using (ui.ModalDialog(PANEL_WIDTH, PANEL_HEIGHT))
        {
            ui.Label(
                _title,
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
                _body,
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
                }
            ))
            {
                ui.Filler();
                if (ui.ActionButton(
                    "Button_Ok",
                    _localization.GetRequired("ui.button.ok"),
                    _okButton with
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
                    menu.GoBack();
                }
            }
        }

        return Result.FromSuccess();
    }

    /// <summary>Prepares the modal for one message.</summary>
    public Modal Create(string title, string body)
    {
        _title = title;
        _body = body;
        return Modal;
    }
}
