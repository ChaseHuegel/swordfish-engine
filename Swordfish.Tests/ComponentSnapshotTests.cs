using System;
using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Components;
using Xunit;

namespace Swordfish.Tests;

public class ComponentSnapshotTests
{
    [Fact]
    public void FoundationComponentsMatchSnapshots()
    {
        Snapshot("component-button-secondary", ui => new Button().Render(
            ui,
            parameters: new ButtonParameters(),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(264),
                Height = new Fixed(44),
            },
            content_default: content => DrawButtonLabel(content, "Secondary")
        ));

        Snapshot("component-button-primary", ui => new Button().Render(
            ui,
            parameters: new ButtonParameters(ButtonVariant.Primary),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(264),
                Height = new Fixed(44),
            },
            content_default: content => DrawButtonLabel(content, "Primary")
        ));

        Snapshot("component-button-compact", ui => new Button().Render(
            ui,
            parameters: new ButtonParameters(ButtonVariant.Secondary, Compact: true),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(116),
                Height = new Fixed(32),
            },
            content_default: content => DrawButtonLabel(content, "Feedback")
        ));

        Snapshot("component-button-disabled", ui => new Button().Render(
            ui,
            parameters: new ButtonParameters(Enabled: false),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(264),
                Height = new Fixed(44),
            },
            content_default: content => DrawButtonLabel(content, "Disabled")
        ));

        Snapshot("component-button-compact-plain", ui => new Button().Render(
            ui,
            parameters: new ButtonParameters(ButtonVariant.Secondary, Compact: true, DoubleBorder: false),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(116),
                Height = new Fixed(32),
            },
            content_default: content => DrawButtonLabel(content, "Feedback", compact: true)
        ));

        Snapshot("component-panel", ui => new Panel().Render(
            ui,
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(400),
                Height = new Fixed(180),
            },
            content_header: content => DrawText(content, "Header"),
            content_default: content => DrawText(content, "Body")
        ));

        Snapshot("component-card-selected", ui => new Card().Render(
            ui,
            parameters: new CardParameters(Selected: true),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(360),
                Height = new Fixed(52),
            },
            content_default: content => DrawText(content, "Card")
        ));

        Snapshot("component-tab", ui => new Tab().Render(
            ui,
            parameters: new TabParameters(),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(160),
                Height = new Fixed(40),
            },
            content_default: content => DrawText(content, "Tab")
        ));

        Snapshot("component-tab-selected", ui => new Tab().Render(
            ui,
            parameters: new TabParameters(Selected: true),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(160),
                Height = new Fixed(40),
            },
            content_default: content => DrawText(content, "Tab")
        ));

        Snapshot("component-card", ui => new Card().Render(
            ui,
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(400),
                Height = new Fixed(80),
            },
            content_leading: content => DrawText(content, "Lead"),
            content_default: content => DrawText(content, "Title"),
            content_trailing: content => DrawText(content, "End")
        ));

        Snapshot("component-selectable-row", ui => new SelectableRow().Render(
            ui,
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(400),
                Height = new Fixed(80),
            },
            content_leading: content => DrawText(content, "Lead"),
            content_default: content => DrawText(content, "Row"),
            content_trailing: content => DrawText(content, "End")
        ));

        Snapshot("component-info-box", ui => new InfoBox().Render(
            ui,
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(400),
                Height = new Fixed(200),
            },
            content_title: content => DrawText(content, "Info"),
            content_default: content => DrawText(content, "Body")
        ));

        Snapshot("component-empty-state", ui => new EmptyState().Render(
            ui,
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(400),
                Height = new Fixed(120),
            },
            content_default: content => DrawText(content, "No entries.")
        ));

        Snapshot("component-keycap", ui => new Keycap().Render(
            ui,
            content_default: content => DrawText(content, "Esc")
        ));

        Snapshot("component-hint-pill", ui => new HintPill().Render(
            ui,
            content_key: content => new Keycap().Render(content, content_default: key => DrawText(key, "Esc")),
            content_default: content => DrawText(content, "Close")
        ));

        Snapshot("component-modal", ui => new Modal().Render(
            ui,
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(520),
                Height = new Fixed(250),
            },
            content_header: content => DrawText(content, "Title"),
            content_default: content => DrawText(content, "Body"),
            content_actions: content => DrawText(content, "Action")
        ));

        Snapshot("component-progress-bar", ui => new ProgressBar().Render(
            ui,
            new ProgressBarState(0.5f),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(400),
                Height = new Fixed(6),
            }
        ));

        Snapshot("component-scroll-bar", ui => new ScrollBar().Render(
            ui,
            new ScrollBarState(0.25f, 0.5f),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(6),
                Height = new Fixed(200),
            }
        ));

        Snapshot("component-scroll-area", ui =>
        {
            var state = new ScrollAreaState();
            state.SetMetrics(viewportHeight: 100, contentHeight: 400);
            new ScrollArea().Render(
                ui,
                state,
                callerConstraints: new ConstraintsOverride
                {
                    Width = new Fixed(300),
                    Height = new Fixed(100),
                },
                content_default: content => DrawText(content, "Item")
            );
        });

        Snapshot("component-item-slot", ui =>
        {
            var texture = new Texture([], 1, 1, mipmaps: false);
            var icon = new Material(null!, texture);
            new ItemSlot().Render(
                ui,
                new ItemSlotState(icon, "1", "64", Selected: true),
                callerConstraints: new ConstraintsOverride
                {
                    Width = new Fixed(48),
                    Height = new Fixed(48),
                }
            );
        });

    }

    private static void Snapshot(string name, Action<UIBuilder<Material>> render)
    {
        UiSnapshotTests.Compare(name, 800, 600, render);
    }

    internal static void DrawButtonLabel(UIBuilder<Material> ui, string text, bool compact = false)
    {
        using (ui.Text(text))
        {
            ui.FontOptions = new FontOptions
            {
                ID = "Salmon Mono 9 Regular",
                Size = compact ? 14 : 18,
                NoWrap = true,
            };
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
        }
    }

    private static void DrawText(UIBuilder<Material> ui, string text)
    {
        using (ui.Text(text))
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }
    }
}
