using System;
using System.Collections.Generic;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using Xunit;

namespace Swordfish.Tests;

public class StarChartViewTests
{
    private const int WIDTH = 711;
    private const int HEIGHT = 400;

    [Fact]
    public void EachNodeDrawsItsNameAndGlyph()
    {
        List<RenderCommand<Material>> commands = Render(
            State(Node("aaaa", "Vela", 200, 200), Node("bbbb", "Antila", 400, 120))
        );

        Assert.NotNull(commands.Find(command => command.Text == "Vela"));
        Assert.NotNull(commands.Find(command => command.Text == "Antila"));
        Assert.Equal(2, commands.FindAll(command => command.Text == "\uf219").Count);
    }

    [Fact]
    public void IdleNodesHideBrackets()
    {
        List<RenderCommand<Material>> commands = Render(State(Node("aaaa", "Vela", 200, 200)));

        Assert.DoesNotContain(commands, command => command.Text == "[");
        Assert.DoesNotContain(commands, command => command.Text == "]");
    }

    [Fact]
    public void SelectedNodeShowsBracketsAndLabelHighlight()
    {
        List<RenderCommand<Material>> commands = Render(State(Node("aaaa", "Vela", 200, 200, selected: true)));

        Assert.NotNull(commands.Find(command => command.Text == "["));
        Assert.NotNull(commands.Find(command => command.Text == "]"));

        RenderCommand<Material> label = Assert.Single(commands, command => command.Text == "Vela");
        Assert.Equal(0.6471f, label.Color.X, 3);
    }

    [Fact]
    public void EmptyChartShowsThePrompt()
    {
        List<RenderCommand<Material>> commands = Render(State());

        RenderCommand<Material> prompt = Assert.Single(
            commands,
            command => command.Text == "ui.label.level.chart.empty"
        );
        Assert.Equal(HEIGHT, prompt.Rect.Size.Y);
    }

    [Fact]
    public void CardStaysHiddenUntilHover()
    {
        List<RenderCommand<Material>> commands = Render(State(Node("aaaa", "Vela", 200, 200)));

        Assert.DoesNotContain(commands, command => command.Text == "ui.label.level.mode");
    }

    [Fact]
    public void HoveredNodeOpensTheCard()
    {
        var controller = new UIController();
        StarChartState state = State(Node("aaaa", "Vela", 200, 200));

        Render(state, controller);
        controller.UpdateMouse(200, 200, UIController.MouseButtons.None);
        List<RenderCommand<Material>> commands = Render(state, controller);

        Assert.NotNull(commands.Find(command => command.Text == "ui.label.level.mode"));
        Assert.NotNull(commands.Find(command => command.Text == "Creative"));
        Assert.NotNull(commands.Find(command => command.Text == "seed phrase"));
        Assert.NotNull(commands.Find(command => command.Text == "["));
        Assert.NotNull(commands.Find(command => command.Text == "]"));
    }

    /// <summary>Only the hit target owns the node id, so hover is unambiguous.</summary>
    [Fact]
    public void OnlyTheHitTargetCarriesTheNodeId()
    {
        List<RenderCommand<Material>> commands = Render(State(Node("aaaa", "Vela", 200, 200)));

        var withId = new List<string>();
        foreach (RenderCommand<Material> command in commands)
        {
            if (command.ID != null)
            {
                withId.Add(command.ID + "@" + command.Rect.Position.X + "," + command.Rect.Position.Y
                    + "," + command.Rect.Size.X + "," + command.Rect.Size.Y);
            }
        }

        Assert.True(withId.Count == 1, "Ids: " + string.Join(" | ", withId));
        Assert.Equal("SelectSave.Node.aaaa@188,188,24,40", withId[0]);
    }

    /// <summary>Hover stays open over the diamond and over the label.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(18)]
    public void HoverKeepsTheCardOpen(int offsetY)
    {
        var controller = new UIController();
        StarChartState state = State(Node("aaaa", "Vela", 200, 200));
        var ui = new UIBuilder<Material>(WIDTH, HEIGHT, new StubTextEngine(), controller);

        for (var frame = 0; frame < 4; frame++)
        {
            controller.UpdateMouse(200, 200 + offsetY, UIController.MouseButtons.None);
            new StarChart().Render(
                ui,
                context: state,
                callerConstraints: new ConstraintsOverride { Width = new Fill(), Height = new Fill() },
                idPrefix: "SelectSave.Chart"
            );

            List<RenderCommand<Material>> commands = ui.Build(0d);
            if (frame >= 2)
            {
                Assert.Contains(commands, command => command.Text == "ui.label.level.mode");
                Assert.Contains(commands, command => command.Text == "[");
            }
        }
    }

    [Fact]
    public void PassthroughNodeStillDraws()
    {
        List<RenderCommand<Material>> commands = Render(
            State(Node("aaaa", "Vela", 200, 200, selected: true, passthrough: true))
        );

        Assert.NotNull(commands.Find(command => command.Text == "Vela"));
        Assert.NotNull(commands.Find(command => command.Text == "\uf219"));
    }

    [Fact]
    public void SpecksDrawAsSinglePixels()
    {
        List<RenderCommand<Material>> commands = Render(State(Node("aaaa", "Vela", 200, 200)));

        int pixels = commands.FindAll(command => command.Rect.Size.X == 1 && command.Rect.Size.Y == 1).Count;
        Assert.Equal(2, pixels);
    }

    [Fact]
    public void StarClassSelectsAColor()
    {
        List<RenderCommand<Material>> violet = Render(State(Node("aaaa", "Vela", 200, 200, violet: true)));
        List<RenderCommand<Material>> emerald = Render(State(Node("aaaa", "Vela", 200, 200, emerald: true)));

        RenderCommand<Material> violetGlyph = Assert.Single(violet, command => command.Text == "\uf219");
        RenderCommand<Material> emeraldGlyph = Assert.Single(emerald, command => command.Text == "\uf219");

        Assert.NotEqual(violetGlyph.Color, emeraldGlyph.Color);
        Assert.Equal(0.7529f, violetGlyph.Color.X, 3);
        Assert.Equal(0.4824f, emeraldGlyph.Color.X, 3);
    }

    private static StarChartState State(params StarChartNodeState[] nodes)
    {
        var specks = new List<StarSpeck>
        {
            new(10, 10),
            new(20, 30),
        };

        return new StarChartState(nodes, specks, new TestLocalization(), _ => { }, Ready: true);
    }

    private static StarChartNodeState Node(
        string guid,
        string name,
        int x,
        int y,
        bool selected = false,
        bool passthrough = false,
        bool red = false,
        bool emerald = false,
        bool violet = false
    )
    {
        int half = StarChartLayout.HIT_WIDTH / 2;
        int glyph = StarChartLayout.GLYPH_SIZE / 2;

        return new StarChartNodeState(
            Guid: guid,
            NodeId: "SelectSave.Node." + guid,
            RowId: "Button_SelectSave_" + name,
            Name: name,
            Mode: "Creative",
            Seed: "seed phrase",
            TimePlayed: "1 hour",
            LastPlayed: "1/1/2026",
            ClassIndex: red ? 1 : emerald ? 2 : violet ? 3 : 0,
            IsRed: red,
            IsEmerald: emerald,
            IsViolet: violet,
            X: x,
            Y: y,
            HitX: x - half,
            HitY: y + StarChartLayout.HIT_OFFSET_Y,
            GlyphX: x - glyph,
            GlyphY: y - glyph,
            BracketLeftX: x + StarChartLayout.BRACKET_OFFSET_LEFT,
            BracketRightX: x + StarChartLayout.BRACKET_OFFSET_RIGHT,
            BracketY: y + StarChartLayout.BRACKET_OFFSET_Y,
            LabelX: x - (StarChartLayout.LABEL_WIDTH / 2),
            LabelY: y + StarChartLayout.LABEL_OFFSET_Y,
            CardX: 100,
            CardY: 100,
            CardHeight: StarChartLayout.CARD_HEIGHT,
            Selected: selected,
            Passthrough: passthrough
        );
    }

    private static List<RenderCommand<Material>> Render(StarChartState state, UIController? controller = null)
    {
        var ui = new UIBuilder<Material>(WIDTH, HEIGHT, new StubTextEngine(), controller ?? new UIController());

        new StarChart().Render(
            ui,
            context: state,
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fill(),
                Height = new Fill(),
            },
            idPrefix: "SelectSave.Chart"
        );

        return ui.Build(0d);
    }
}
