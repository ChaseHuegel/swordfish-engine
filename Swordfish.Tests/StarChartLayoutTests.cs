using System;
using System.Collections.Generic;
using System.Globalization;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using Xunit;

namespace Swordfish.Tests;

public class StarChartLayoutTests
{
    private const int WIDTH = 711;
    private const int HEIGHT = 400;
    private const long SEED = 12345;

    [Fact]
    public void BasePositionIsDeterministic()
    {
        StarChartGeometry first = StarChartLayout.Compute(Saves(("a", SEED)), null, WIDTH, HEIGHT);
        StarChartGeometry second = StarChartLayout.Compute(Saves(("a", SEED)), null, WIDTH, HEIGHT);

        Assert.Equal(first.Nodes[0].X, second.Nodes[0].X);
        Assert.Equal(first.Nodes[0].Y, second.Nodes[0].Y);
    }

    [Fact]
    public void BasePositionStaysInsideNormalizedRange()
    {
        for (var i = 0; i < 10_000; i++)
        {
            double x = StarChartLayout.NormalizedX(i);
            double y = StarChartLayout.NormalizedY(i);

            Assert.InRange(x, 0.14, 0.86);
            Assert.InRange(y, 0.18, 0.84);
        }
    }

    [Fact]
    public void NegativeSeedsPlaceNormally()
    {
        var saves = new List<StarChartSource>
        {
            new("a", long.MinValue),
            new("b", -1),
            new("c", long.MaxValue),
        };

        StarChartGeometry geometry = StarChartLayout.Compute(saves, null, WIDTH, HEIGHT);

        Assert.Equal(3, geometry.Nodes.Count);
        foreach (StarNodePlacement node in geometry.Nodes)
        {
            Assert.InRange(node.X, StarChartLayout.NODE_INSET_X, WIDTH - StarChartLayout.NODE_INSET_X);
            Assert.InRange(node.Y, StarChartLayout.NODE_INSET_Y, HEIGHT - StarChartLayout.NODE_INSET_Y);
            Assert.InRange(node.ClassIndex, 0, StarChartLayout.STAR_CLASS_COUNT - 1);
        }
    }

    [Fact]
    public void EqualSeedsShareAClass()
    {
        Assert.Equal(StarChartLayout.ClassOf(SEED), StarChartLayout.ClassOf(SEED));
    }

    [Fact]
    public void ClassIsAlwaysInRange()
    {
        for (var i = 0; i < 1_000; i++)
        {
            int starClass = StarChartLayout.ClassOf(i);
            Assert.InRange(starClass, 0, StarChartLayout.STAR_CLASS_COUNT - 1);
        }
    }

    [Fact]
    public void EqualSeedsSeparate()
    {
        var saves = new List<StarChartSource>
        {
            new("aaaaaaaa", SEED),
            new("bbbbbbbb", SEED),
        };

        StarChartGeometry geometry = StarChartLayout.Compute(saves, null, WIDTH, HEIGHT);

        Assert.True(Distance(geometry.Nodes[0], geometry.Nodes[1]) >= StarChartLayout.NODE_MIN_DISTANCE);
    }

    [Fact]
    public void SeparationDirectionIsDeterministic()
    {
        var saves = new List<StarChartSource>
        {
            new("aaaaaaaa", SEED),
            new("bbbbbbbb", SEED),
        };

        StarChartGeometry first = StarChartLayout.Compute(saves, null, WIDTH, HEIGHT);
        StarChartGeometry second = StarChartLayout.Compute(saves, null, WIDTH, HEIGHT);

        Assert.Equal(first.Nodes, second.Nodes);
    }

    [Fact]
    public void SeparationStepIsDeterministicAndSized()
    {
        for (var attempt = 0; attempt < 16; attempt++)
        {
            StarChartLayout.SeparationStep("aaaaaaaa", attempt, out int dx, out int dy);
            StarChartLayout.SeparationStep("aaaaaaaa", attempt, out int againX, out int againY);

            Assert.Equal(dx, againX);
            Assert.Equal(dy, againY);

            double length = Math.Sqrt((dx * dx) + (dy * dy));
            Assert.InRange(length, StarChartLayout.NODE_MIN_DISTANCE - 1.5, StarChartLayout.NODE_MIN_DISTANCE + 1.5);
        }
    }

    [Fact]
    public void SeparationMovesTheSecondNodeOffItsBase()
    {
        var saves = new List<StarChartSource>
        {
            new("00000000", SEED),
            new("ffffffff", SEED),
        };

        StarChartGeometry geometry = StarChartLayout.Compute(saves, null, 4000, 4000);
        StarNodePlacement second = geometry.Nodes[1];
        var baseNode = new StarNodePlacement("x", BaseX(SEED, 4000), BaseY(SEED, 4000), 0, false, false, 0, 0);

        Assert.True(Distance(second, baseNode) >= StarChartLayout.NODE_MIN_DISTANCE - 1.5);
    }

    [Fact]
    public void SeparationIsBoundedByAttempts()
    {
        var saves = new List<StarChartSource>(StarChartLayout.MAX_SEPARATION_ATTEMPTS + 4);
        for (var i = 0; i < saves.Capacity; i++)
        {
            saves.Add(new StarChartSource(i.ToString("D8", CultureInfo.InvariantCulture), SEED));
        }

        StarChartGeometry geometry = StarChartLayout.Compute(saves, null, 120, 90);

        Assert.Equal(saves.Count, geometry.Nodes.Count);
        foreach (StarNodePlacement node in geometry.Nodes)
        {
            Assert.InRange(node.X, 0, 120);
            Assert.InRange(node.Y, 0, 90);
        }
    }

    [Fact]
    public void NodesStayInsideTheClampRectangle()
    {
        var saves = new List<StarChartSource>();
        for (var i = 0; i < 200; i++)
        {
            saves.Add(new StarChartSource(i.ToString("D8", CultureInfo.InvariantCulture), i * 37));
        }

        StarChartGeometry geometry = StarChartLayout.Compute(saves, null, WIDTH, HEIGHT);

        foreach (StarNodePlacement node in geometry.Nodes)
        {
            Assert.InRange(node.X, StarChartLayout.NODE_INSET_X, WIDTH - StarChartLayout.NODE_INSET_X);
            Assert.InRange(node.Y, StarChartLayout.NODE_INSET_Y, HEIGHT - StarChartLayout.NODE_INSET_Y);
        }
    }

    [Fact]
    public void PlayOrderDoesNotMoveNodes()
    {
        var first = new List<StarChartSource>
        {
            new("00000001", 11),
            new("00000002", 22),
            new("00000003", 33),
        };
        var reordered = new List<StarChartSource> { first[2], first[0], first[1] };

        StarChartGeometry a = StarChartLayout.Compute(first, null, WIDTH, HEIGHT);
        StarChartGeometry b = StarChartLayout.Compute(reordered, null, WIDTH, HEIGHT);

        for (var i = 0; i < a.Nodes.Count; i++)
        {
            Assert.Equal(a.Nodes[i].Guid, b.Nodes[i].Guid);
            Assert.Equal(a.Nodes[i].X, b.Nodes[i].X);
            Assert.Equal(a.Nodes[i].Y, b.Nodes[i].Y);
        }
    }

    [Fact]
    public void AddingAClearSaveDoesNotMoveOthers()
    {
        var baseline = new List<StarChartSource>
        {
            new("00000001", 11),
            new("00000002", 22),
        };
        var withExtra = new List<StarChartSource>(baseline) { new("ffffffff", 99) };

        StarChartGeometry a = StarChartLayout.Compute(baseline, null, WIDTH, HEIGHT);
        StarChartGeometry b = StarChartLayout.Compute(withExtra, null, WIDTH, HEIGHT);

        for (var i = 0; i < a.Nodes.Count; i++)
        {
            Assert.Equal(a.Nodes[i].X, b.Nodes[i].X);
            Assert.Equal(a.Nodes[i].Y, b.Nodes[i].Y);
        }
    }

    [Fact]
    public void OverlapUsesSquareHitBoxes()
    {
        Assert.True(StarChartLayout.Overlaps(0, 0, 20, 20));
        Assert.True(StarChartLayout.Overlaps(0, 0, 0, 23));
        Assert.False(StarChartLayout.Overlaps(0, 0, 24, 0));
        Assert.False(StarChartLayout.Overlaps(0, 0, 28, 0));
    }

    [Fact]
    public void UnselectedNodesNeverPassThrough()
    {
        var saves = new List<StarChartSource>
        {
            new("aaaaaaaa", SEED),
            new("bbbbbbbb", SEED),
        };

        StarChartGeometry geometry = StarChartLayout.Compute(saves, null, WIDTH, HEIGHT);

        Assert.All(geometry.Nodes, node => Assert.False(node.Passthrough));
    }

    [Fact]
    public void SelectedOverlappingNodePassesThrough()
    {
        var saves = new List<StarChartSource>
        {
            new("aaaaaaaa", SEED),
            new("bbbbbbbb", SEED),
        };

        StarChartGeometry geometry = StarChartLayout.Compute(saves, "aaaaaaaa", 200, 120);

        Assert.True(geometry.Nodes[0].Passthrough || geometry.Nodes[1].Passthrough);
    }

    [Fact]
    public void SelectedIsolatedNodeKeepsClick()
    {
        var saves = new List<StarChartSource>
        {
            new("aaaaaaaa", 11),
            new("bbbbbbbb", 22),
            new("cccccccc", 33),
        };

        StarChartGeometry geometry = StarChartLayout.Compute(saves, "aaaaaaaa", WIDTH, HEIGHT);
        StarNodePlacement selected = Assert.Single(geometry.Nodes, node => node.Selected);

        bool overlaps = false;
        foreach (StarNodePlacement node in geometry.Nodes)
        {
            if (node.Guid != selected.Guid && StarChartLayout.Overlaps(selected.X, selected.Y, node.X, node.Y))
            {
                overlaps = true;
            }
        }

        Assert.Equal(overlaps, selected.Passthrough);
    }

    [Theory]
    [InlineData(0, 0, 310, 132)]
    [InlineData(300, 0, 310, 132)]
    [InlineData(711, 400, 310, 132)]
    public void CardStaysInsideTheField(int nodeX, int nodeY, int cardWidth, int cardHeight)
    {
        int x = StarChartLayout.CardX(nodeX, WIDTH);
        int y = StarChartLayout.CardY(nodeY, HEIGHT);

        Assert.InRange(x, 0, WIDTH - cardWidth);
        Assert.InRange(y, 0, HEIGHT - cardHeight);
    }

    [Fact]
    public void CardClampsOnNarrowFields()
    {
        for (var nodeX = 0; nodeX <= 456; nodeX += 8)
        {
            int x = StarChartLayout.CardX(nodeX, 456);
            Assert.InRange(x, 0, Math.Max(0, 456 - StarChartLayout.CARD_WIDTH));
        }
    }

    [Fact]
    public void EmptyInputReturnsEmptyGeometry()
    {
        Assert.Empty(StarChartLayout.Compute(Array.Empty<StarChartSource>(), null, WIDTH, HEIGHT).Nodes);

        StarChartGeometry collapsed = StarChartLayout.Compute(Saves(("a", SEED)), null, 0, 0);
        Assert.Empty(collapsed.Nodes);
        Assert.Empty(collapsed.Specks);
    }

    [Fact]
    public void SpecksAreDeterministicAndCapped()
    {
        StarChartGeometry first = StarChartLayout.Compute(Saves(("a", SEED)), null, WIDTH, HEIGHT);
        StarChartGeometry second = StarChartLayout.Compute(Saves(("a", SEED)), null, WIDTH, HEIGHT);

        Assert.Equal(first.Specks, second.Specks);
        Assert.InRange(first.Specks.Count, 0, StarChartLayout.MAX_SPECKS);

        foreach (StarSpeck speck in first.Specks)
        {
            Assert.InRange(speck.X, 0, WIDTH - 1);
            Assert.InRange(speck.Y, 0, HEIGHT - 1);
        }
    }

    [Fact]
    public void SelectionMarksOneNode()
    {
        var saves = new List<StarChartSource>
        {
            new("aaaaaaaa", 11),
            new("bbbbbbbb", 22),
        };

        StarChartGeometry geometry = StarChartLayout.Compute(saves, "bbbbbbbb", WIDTH, HEIGHT);
        StarNodePlacement selected = Assert.Single(geometry.Nodes, node => node.Selected);

        Assert.Equal("bbbbbbbb", selected.Guid);
    }

    private static List<StarChartSource> Saves(params (string Guid, long Seed)[] saves)
    {
        var list = new List<StarChartSource>(saves.Length);
        foreach ((string guid, long seed) in saves)
        {
            list.Add(new StarChartSource(guid, seed));
        }

        return list;
    }

    private static int BaseX(long seed, int width)
    {
        return StarChartLayout.ClampX((int)Math.Round(StarChartLayout.NormalizedX(seed) * width), width);
    }

    private static int BaseY(long seed, int height)
    {
        return StarChartLayout.ClampY((int)Math.Round(StarChartLayout.NormalizedY(seed) * height), height);
    }

    private static double Distance(in StarNodePlacement a, in StarNodePlacement b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
