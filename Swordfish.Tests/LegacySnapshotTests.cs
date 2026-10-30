using System;
using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Theme;
using Xunit;

namespace Swordfish.Tests;

public class LegacySnapshotTests
{
    [Fact]
    public void LegacyMainMenuOverlayIsCaptured()
    {
        int[] widths = [800, 1280, 1920];
        int[] heights = [600, 720, 1080];
        for (var i = 0; i < widths.Length; i++)
        {
            int width = widths[i];
            int height = heights[i];
            UiSnapshotTests.Compare($"{width}x{height}-main-menu-overlay", width, height, ui =>
            {
                using (ui.Element())
                {
                    ui.Padding = new Padding(20);
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Bottom | Anchors.Left,
                    };
                    ui.ActionButton(
                        "Button_Feedback",
                        "Send feedback",
                        Widgets.ActionButtonOptions.Compact(ButtonVariant.Secondary, null)
                    );
                }

                using (ui.Row(
                    new Constraints
                    {
                        Anchors = Anchors.Bottom | Anchors.Right,
                        X = new Fixed(-20),
                        Y = new Fixed(-20),
                        Width = new Fixed(88),
                        Height = new Fixed(40),
                    },
                    spacing: 8
                ))
                {
                    var options = new Widgets.IconButtonOptions(
                        FontId: UiTheme.FONT_BRANDS,
                        FontSize: UiMetrics.LARGE_ICON_FONT
                    );
                    ui.IconButton(
                        "Button_Discord",
                        MenuGlyphs.DISCORD,
                        options,
                        new Constraints { Width = new Fixed(40), Height = new Fixed(40) }
                    );
                    ui.IconButton(
                        "Button_Steam",
                        MenuGlyphs.STEAM,
                        options,
                        new Constraints { Width = new Fixed(40), Height = new Fixed(40) }
                    );
                }
            }, "Legacy/Pages", allowUpdate: false);
        }
    }

    [Fact]
    public void LegacyWidgetsAreCaptured()
    {
        UiSnapshotTests.Compare("legacy-button-secondary", 400, 100, ui =>
        {
            Widgets.ActionButtonOptions options = Widgets.ActionButtonOptions.Menu(
                ButtonVariant.Secondary,
                soundEffectService: null
            ) with
            {
                Layout = new Constraints { Width = new Fixed(264), Height = new Fixed(44) },
                Rect = null,
            };
            ui.ActionButton("legacy_button", "Secondary", options);
        }, "Legacy", allowUpdate: false);

        UiSnapshotTests.Compare("legacy-button-primary", 400, 100, ui =>
        {
            Widgets.ActionButtonOptions options = Widgets.ActionButtonOptions.Menu(
                ButtonVariant.Primary,
                soundEffectService: null
            ) with
            {
                Layout = new Constraints { Width = new Fixed(264), Height = new Fixed(44) },
                Rect = null,
            };
            ui.ActionButton("legacy_button", "Primary", options);
        }, "Legacy", allowUpdate: false);

        UiSnapshotTests.Compare("legacy-panel", 400, 300, ui =>
        {
            using (ui.MenuPanel(
                new Constraints { Width = new Fixed(360), Height = new Fixed(240) },
                "Panel"
            ))
            {
                ui.Label(
                    "Body",
                    UiMetrics.MENU_BODY_FONT,
                    UiTheme.Text,
                    new Constraints { Width = new Fill(), Height = new Fill() }
                );
            }
        }, "Legacy", allowUpdate: false);

        UiSnapshotTests.Compare("legacy-info-box", 400, 200, ui =>
        {
            ui.InfoBox(
                new Constraints { Width = new Fixed(360), Height = new Fixed(160) },
                "Info",
                "Body"
            );
        }, "Legacy", allowUpdate: false);

        UiSnapshotTests.Compare("legacy-tab", 400, 100, ui =>
        {
            ui.Tab(
                "legacy_tab",
                "Tab",
                selected: true,
                new Constraints { Width = new Fixed(160), Height = new Fixed(40) }
            );
        }, "Legacy", allowUpdate: false);

        UiSnapshotTests.Compare("legacy-selectable-card", 400, 100, ui =>
        {
            ui.SelectableCard(
                "legacy_card",
                new Constraints { Width = new Fixed(360), Height = new Fixed(52) },
                new Widgets.SelectableCardOptions(Selected: true, Enabled: true),
                contentUi =>
                {
                    contentUi.Label(
                        "Card",
                        UiMetrics.MENU_BODY_FONT,
                        UiTheme.Text,
                        new Constraints { Width = new Fill(), Height = new Fill() }
                    );
                }
            );
        }, "Legacy", allowUpdate: false);

        UiSnapshotTests.Compare("legacy-menu-text-box", 400, 100, ui =>
        {
            var state = new TextBoxState("Text");
            var options = new TextBoxState.Options(
                Constraints: new Constraints { Width = new Fixed(300), Height = new Fixed(32) }
            );
            state = new TextBoxState("Text", options);
            ui.MenuTextBox(
                "legacy_text_box",
                ref state,
                new Constraints { Width = new Fixed(300), Height = new Fixed(32) },
                new FontOptions { Size = 18 },
                new FakeInputService()
            );
        }, "Legacy", allowUpdate: false);

        UiSnapshotTests.Compare("legacy-dotted-divider", 400, 100, ui =>
        {
            ui.DottedDivider(Widgets.Rect(0, 0, 300, 1));
        }, "Legacy", allowUpdate: false);
    }
}
