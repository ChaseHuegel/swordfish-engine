using System;
using System.Collections.Generic;
using System.Linq;
using Swordfish.UI.Reef;
using Xunit;

namespace Swordfish.Tests;

public class ReefAtlasTests
{
    private static ReefAtlas.Source Source(string key, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i % 251);
        }

        return new ReefAtlas.Source(key, width, height, pixels);
    }

    [Fact]
    public void EverySourceGetsARegionInsideTheAtlas()
    {
        ReefAtlas atlas = ReefAtlas.Pack([
            Source("a", 64, 64),
            Source("b", 128, 32),
            Source("c", 16, 200),
        ]);

        Assert.Equal(atlas.Width * atlas.Height * 4, atlas.Pixels.Length);

        foreach (string key in new[] { "a", "b", "c" })
        {
            Assert.True(atlas.TryGetRegion(key, out ReefAtlas.Region region));
            Assert.True(region.OffsetX >= 0);
            Assert.True(region.OffsetY >= 0);
            Assert.True(region.OffsetX + region.Width <= atlas.Width);
            Assert.True(region.OffsetY + region.Height <= atlas.Height);
        }
    }

    [Fact]
    public void RegionsDoNotOverlap()
    {
        ReefAtlas atlas = ReefAtlas.Pack([
            Source("a", 64, 64),
            Source("b", 128, 32),
            Source("c", 16, 200),
            Source("d", 32, 48),
        ]);

        List<ReefAtlas.Region> regions = new[] { "a", "b", "c", "d" }
            .Select(atlas.GetRegion)
            .ToList();

        for (var i = 0; i < regions.Count; i++)
        {
            for (var j = i + 1; j < regions.Count; j++)
            {
                bool overlaps =
                    regions[i].OffsetX < regions[j].OffsetX + regions[j].Width &&
                    regions[j].OffsetX < regions[i].OffsetX + regions[i].Width &&
                    regions[i].OffsetY < regions[j].OffsetY + regions[j].Height &&
                    regions[j].OffsetY < regions[i].OffsetY + regions[i].Height;

                Assert.False(overlaps, $"Region {i} overlaps region {j}.");
            }
        }
    }

    [Fact]
    public void PixelsLandAtTheRegionOffset()
    {
        const int WIDTH = 3;
        const int HEIGHT = 2;
        var pixels = new byte[WIDTH * HEIGHT * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i + 1);
        }

        var source = new ReefAtlas.Source("only", WIDTH, HEIGHT, pixels);
        ReefAtlas atlas = ReefAtlas.Pack([source]);
        ReefAtlas.Region region = atlas.GetRegion("only");

        for (var row = 0; row < HEIGHT; row++)
        {
            for (var column = 0; column < WIDTH * 4; column++)
            {
                int sourceIndex = (row * WIDTH * 4) + column;
                int atlasIndex = (((region.OffsetY + row) * atlas.Width) + region.OffsetX) * 4 + column;
                Assert.Equal(pixels[sourceIndex], atlas.Pixels[atlasIndex]);
            }
        }
    }

    [Fact]
    public void PackingIsCompact()
    {
        ReefAtlas.Source[] sources =
        [
            Source("a", 100, 100),
            Source("b", 100, 100),
            Source("c", 100, 100),
            Source("d", 100, 100),
        ];

        long area = sources.Sum(source => (long)source.Width * source.Height);
        ReefAtlas atlas = ReefAtlas.Pack(sources);

        //  Four equal squares pack into a square without waste.
        Assert.Equal(200, atlas.Width);
        Assert.Equal(200, atlas.Height);
        Assert.Equal(area, (long)atlas.Width * atlas.Height);
    }

    [Fact]
    public void EmptySourceListIsRejected()
    {
        Assert.Throws<ArgumentException>(() => ReefAtlas.Pack([]));
    }

    [Fact]
    public void RegionMappingSpansThePackedImage()
    {
        ReefAtlas atlas = ReefAtlas.Pack([
            Source("a", 64, 64),
            Source("b", 32, 128),
        ]);

        ReefAtlas.Region region = atlas.GetRegion("a");

        float left = region.MapU(0, atlas.Width);
        float right = region.MapU(region.Width, atlas.Width);
        float top = region.MapV(0, atlas.Height);
        float bottom = region.MapV(region.Height, atlas.Height);

        Assert.Equal(region.OffsetX / (float)atlas.Width, left, 6);
        Assert.Equal((region.OffsetX + region.Width) / (float)atlas.Width, right, 6);
        Assert.Equal((region.OffsetY + region.Height) / (float)atlas.Height, top, 6);
        Assert.Equal(region.OffsetY / (float)atlas.Height, bottom, 6);

        //  Source rows count down from the top of the image.
        Assert.True(top > bottom);
    }
}
