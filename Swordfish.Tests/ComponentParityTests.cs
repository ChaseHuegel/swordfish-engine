using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Theme;
using Xunit;

namespace Swordfish.Tests;

public class ComponentParityTests
{
    [Fact]
    public void SecondaryButtonMatchesLegacy()
    {
        string legacy = UiSnapshotTests.CaptureRender(264, 44, ui =>
        {
            ui.ActionButton(
                "legacy_button",
                "Secondary",
                Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, null) with
                {
                    Layout = new Constraints { Width = new Fixed(264), Height = new Fixed(44) },
                }
            );
        });

        string component = UiSnapshotTests.CaptureRender(264, 44, ui => new Button().Render(
            ui,
            parameters: new ButtonParameters(),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(264),
                Height = new Fixed(44),
            },
            content_default: content => ComponentSnapshotTests.DrawButtonLabel(content, "Secondary")
        ));

        Assert.Equal(UiSnapshotTests.NormalizeIds(legacy), UiSnapshotTests.NormalizeIds(component));
    }

    [Fact]
    public void PrimaryButtonMatchesLegacy()
    {
        string legacy = UiSnapshotTests.CaptureRender(264, 44, ui =>
        {
            ui.ActionButton(
                "legacy_button",
                "Primary",
                Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, null) with
                {
                    Layout = new Constraints { Width = new Fixed(264), Height = new Fixed(44) },
                }
            );
        });

        string component = UiSnapshotTests.CaptureRender(264, 44, ui => new Button().Render(
            ui,
            parameters: new ButtonParameters(ButtonVariant.Primary),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(264),
                Height = new Fixed(44),
            },
            content_default: content => ComponentSnapshotTests.DrawButtonLabel(content, "Primary")
        ));

        Assert.Equal(UiSnapshotTests.NormalizeIds(legacy), UiSnapshotTests.NormalizeIds(component));
    }

    [Fact]
    public void CompactButtonMatchesLegacy()
    {
        string legacy = UiSnapshotTests.CaptureRender(116, 32, ui =>
        {
            ui.ActionButton(
                "legacy_button",
                "Feedback",
                Widgets.ActionButtonOptions.Compact(ButtonVariant.Secondary, null) with
                {
                    Layout = new Constraints { Width = new Fixed(116), Height = new Fixed(32) },
                }
            );
        });

        string component = UiSnapshotTests.CaptureRender(116, 32, ui => new Button().Render(
            ui,
            parameters: new ButtonParameters(ButtonVariant.Secondary, Compact: true, DoubleBorder: false),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(116),
                Height = new Fixed(32),
            },
            content_default: content => ComponentSnapshotTests.DrawButtonLabel(content, "Feedback", compact: true)
        ));

        Assert.Equal(UiSnapshotTests.NormalizeIds(legacy), UiSnapshotTests.NormalizeIds(component));
    }

    [Fact]
    public void PanelMatchesLegacy()
    {
        string legacy = UiSnapshotTests.CaptureRender(400, 300, ui =>
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
        });

        string component = UiSnapshotTests.CaptureRender(400, 300, ui => new Panel().Render(
            ui,
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(360),
                Height = new Fixed(240),
            },
            content_header: content => content.Label(
                "Panel",
                UiMetrics.SMALL_FONT,
                UiTheme.TextMuted,
                new Constraints { Width = new Fill(), Height = new Fixed(18) },
                HorizontalAlignment.Center
            ),
            content_default: content => content.Label(
                "Body",
                UiMetrics.MENU_BODY_FONT,
                UiTheme.Text,
                new Constraints { Width = new Fill(), Height = new Fill() }
            )
        ));

        Assert.Equal(UiSnapshotTests.NormalizeIds(legacy), UiSnapshotTests.NormalizeIds(component));
    }

    [Fact]
    public void SelectedTabMatchesLegacy()
    {
        string legacy = UiSnapshotTests.CaptureRender(400, 100, ui =>
        {
            ui.Tab(
                "legacy_tab",
                "Tab",
                selected: true,
                new Constraints { Width = new Fixed(160), Height = new Fixed(40) }
            );
        });

        string component = UiSnapshotTests.CaptureRender(400, 100, ui => new Tab().Render(
            ui,
            parameters: new TabParameters(Selected: true),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(160),
                Height = new Fixed(40),
            },
            content_default: content => content.Label(
                "Tab",
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.AccentBright,
                new Constraints { Width = new Fill(), Height = new Fill() },
                HorizontalAlignment.Center
            )
        ));

        Assert.Equal(UiSnapshotTests.NormalizeVisual(legacy), UiSnapshotTests.NormalizeVisual(component));
    }

    [Fact]
    public void SelectedCardMatchesLegacy()
    {
        string legacy = UiSnapshotTests.CaptureRender(400, 100, ui =>
        {
            ui.SelectableCard(
                "legacy_card",
                new Constraints { Width = new Fixed(360), Height = new Fixed(52) },
                new Widgets.SelectableCardOptions(Selected: true, Enabled: true),
                contentUi => contentUi.Label(
                    "Card",
                    UiMetrics.MENU_BODY_FONT,
                    UiTheme.Text,
                    new Constraints { Width = new Fill(), Height = new Fill() }
                )
            );
        });

        string component = UiSnapshotTests.CaptureRender(400, 100, ui => new Card().Render(
            ui,
            parameters: new CardParameters(Selected: true),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(360),
                Height = new Fixed(52),
            },
            content_default: content => content.Label(
                "Card",
                UiMetrics.MENU_BODY_FONT,
                UiTheme.Text,
                new Constraints { Width = new Fill(), Height = new Fill() }
            )
        ));

        Assert.Equal(UiSnapshotTests.NormalizeVisual(legacy), UiSnapshotTests.NormalizeVisual(component));
    }

    [Fact]
    public void DottedDividerMatchesLegacy()
    {
        string legacy = UiSnapshotTests.CaptureRender(300, 1, ui =>
        {
            ui.DottedDivider(Widgets.Rect(0, 0, 300, 1));
        });

        string component = UiSnapshotTests.CaptureRender(300, 1, ui => new DottedDivider().Render(
            ui,
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(300),
                Height = new Fixed(1),
            }
        ));

        Assert.Equal(UiSnapshotTests.NormalizeVisual(legacy), UiSnapshotTests.NormalizeVisual(component));
    }

    [Fact]
    public void InfoBoxMatchesLegacy()
    {
        string legacy = UiSnapshotTests.CaptureRender(400, 200, ui =>
        {
            ui.InfoBox(
                new Constraints { Width = new Fixed(360), Height = new Fixed(160) },
                "Info",
                "Body"
            );
        });

        string component = UiSnapshotTests.CaptureRender(400, 200, ui => new InfoBox().Render(
            ui,
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(360),
                Height = new Fixed(160),
            },
            content_title: content => content.Label(
                "Info",
                UiMetrics.INFO_BOX_TITLE_FONT,
                UiTheme.Text,
                new Constraints { Width = new Fill(), Height = new Fixed(16) }
            ),
            content_default: content => content.Paragraph(
                "Body",
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                new Constraints { Width = new Fill(), Height = new Fill() }
            )
        ));

        Assert.Equal(UiSnapshotTests.NormalizeIds(legacy), UiSnapshotTests.NormalizeIds(component));
    }
}
