using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Components;
using Xunit;

namespace Swordfish.Tests;

public class ComponentSnapshotTests
{
    [Fact]
    public void FoundationComponentsMatchSnapshots()
    {
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
