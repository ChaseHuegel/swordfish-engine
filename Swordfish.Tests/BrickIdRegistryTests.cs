using System;
using System.Linq;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay;
using Xunit;

namespace Swordfish.Tests;

public class BrickIdRegistryTests
{
    [Fact]
    public void AssignsDistinctNonZeroIdsByOrdinalNameOrder()
    {
        //  Input order must not matter; ids are assigned in ordinal name order.
        BrickIdRegistry registry = BrickIdRegistry.FromNames(["wb:rock", "wb:core", "wb:ice"]);

        Assert.True(registry.TryId("wb:core", out ushort core));
        Assert.True(registry.TryId("wb:ice", out ushort ice));
        Assert.True(registry.TryId("wb:rock", out ushort rock));

        //  Id 0 is reserved for the empty/air voxel, so every brick id starts at 1.
        Assert.NotEqual((ushort)0, core);
        Assert.NotEqual((ushort)0, ice);
        Assert.NotEqual((ushort)0, rock);

        ushort[] ids = new[] { core, ice, rock }.Distinct().OrderBy(id => id).ToArray();
        Assert.Equal(3, ids.Length);

        //  Ordinal ordering: "wb:core" < "wb:ice" < "wb:rock" -> ascending ids.
        Assert.True(core < ice);
        Assert.True(ice < rock);
    }

    [Fact]
    public void IsDeterministicForTheSameNameSet()
    {
        BrickIdRegistry first = BrickIdRegistry.FromNames(["wb:rock", "wb:ice", "wb:core"]);
        BrickIdRegistry second = BrickIdRegistry.FromNames(["wb:ice", "wb:core", "wb:rock"]);

        Assert.Equal(first.Name(1), second.Name(1));
        Assert.Equal(3, first.Count);
        Assert.Equal(first.Count, second.Count);

        foreach (string name in new[] { "wb:rock", "wb:ice", "wb:core" })
        {
            Assert.True(first.TryId(name, out ushort a));
            Assert.True(second.TryId(name, out ushort b));
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void IdsAreSequentialFromMinDataId()
    {
        BrickIdRegistry registry = BrickIdRegistry.FromNames(["a", "b", "c"]);

        Assert.NotNull(registry.Name(1));
        Assert.Equal("a", registry.Name(SaveVersion.MinDataId));
        Assert.Equal("b", registry.Name(SaveVersion.MinDataId + 1));
        Assert.Equal("c", registry.Name(SaveVersion.MinDataId + 2));
    }

    [Fact]
    public void NameAndIdRoundTrip()
    {
        BrickIdRegistry registry = BrickIdRegistry.FromNames(["wb:panel", "wb:rock", "wb:core"]);
        foreach (ushort id in registry.Ids)
        {
            string name = registry.Name(id);
            Assert.NotNull(name);
            Assert.True(registry.TryId(name, out ushort roundTrip));
            Assert.Equal(id, roundTrip);
        }
    }

    [Fact]
    public void DeduplicatesNamesAndIgnoresUnknownLookups()
    {
        BrickIdRegistry registry = BrickIdRegistry.FromNames(["wb:rock", "wb:rock", "wb:ice"]);

        Assert.Equal(2, registry.Count);
        Assert.False(registry.TryId("missing", out _));
        Assert.Null(registry.Name(ushort.MaxValue));
        Assert.True(registry.Name(SaveVersion.MinDataId) == "wb:ice" || registry.Name(SaveVersion.MinDataId) == "wb:rock");
    }
}