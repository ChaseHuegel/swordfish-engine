using System;
using System.Collections.Generic;
using Swordfish.ECS;
using Xunit;

namespace Swordfish.Tests;

public class DataStoreUuidTests
{
    [Fact]
    public void AllocAssignsUniqueUuids()
    {
        DataStore store = new();
        int first = store.Alloc();
        int second = store.Alloc();

        Assert.NotEqual(store.GetUuid(first), Uuid.Null);
        Assert.NotEqual(store.GetUuid(first), store.GetUuid(second));
    }

    [Fact]
    public void FreedEntityUuidIsNotReused()
    {
        DataStore store = new();
        int first = store.Alloc();
        Uuid firstUuid = store.GetUuid(first);
        store.Free(first);

        int second = store.Alloc();

        Assert.Equal(first, second);
        Assert.NotEqual(firstUuid, store.GetUuid(second));
    }

    [Fact]
    public void TryGetByUuidResolvesToSlot()
    {
        DataStore store = new();
        int entity = store.Alloc();
        Uuid uuid = store.GetUuid(entity);

        Assert.True(store.TryGet(uuid, out int resolved));
        Assert.Equal(entity, resolved);
    }

    [Fact]
    public void TryGetUnknownUuidReturnsFalse()
    {
        DataStore store = new();
        store.Alloc();

        Assert.False(store.TryGet(Uuid.NewUuid(), out int _));
    }

    [Fact]
    public void GetUuidOnFreedSlotReturnsNull()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.Free(entity);

        Assert.Equal(Uuid.Null, store.GetUuid(entity));
    }

    [Fact]
    public void AllocWithUuidRestoresIdentity()
    {
        DataStore store = new();
        Uuid uuid = Uuid.NewUuid();
        int entity = store.Alloc(uuid);

        Assert.Equal(uuid, store.GetUuid(entity));
        Assert.True(store.TryGet(uuid, out int resolved));
        Assert.Equal(entity, resolved);
    }

    [Fact]
    public void AllocWithDuplicateUuidThrows()
    {
        DataStore store = new();
        Uuid uuid = Uuid.NewUuid();
        store.Alloc(uuid);

        Assert.Throws<InvalidOperationException>(() => store.Alloc(uuid));
    }

    [Fact]
    public void UuidIdentitySpansChunkBoundaries()
    {
        DataStore store = new(chunkBitWidth: 4);
        List<Uuid> uuids = [];
        for (var i = 0; i < 20; i++)
        {
            int entity = store.Alloc();
            uuids.Add(store.GetUuid(entity));
        }

        for (var i = 0; i < uuids.Count; i++)
        {
            Assert.True(store.TryGet(uuids[i], out int slot));
            Assert.Equal(i + 1, slot);
        }
    }

    [Fact]
    public void EntityExposesUuidAndConverts()
    {
        DataStore store = new();
        Entity entity = new(store.Alloc(), store);

        Assert.Equal(store.GetUuid(entity), entity.Uuid);
        Uuid uuid = entity;
        Assert.Equal(entity.Uuid, uuid);
    }

    [Fact]
    public void UuidEqualityAndHashCode()
    {
        Uuid a = Uuid.NewUuid();
        Uuid b = Uuid.NewUuid();
        Uuid c = a;

        Assert.True(a == c);
        Assert.False(a == b);
        Assert.Equal(a.GetHashCode(), c.GetHashCode());

        Dictionary<Uuid, int> map = new() { [a] = 42 };
        Assert.Equal(42, map[a]);
        Assert.False(map.ContainsKey(b));
    }

    [Fact]
    public void UuidFromValueRoundTrip()
    {
        Uuid uuid = Uuid.NewUuid();

        Uuid restored = Uuid.FromValue(uuid.ToValue());

        Assert.Equal(uuid, restored);
    }

    [Fact]
    public void UuidRestoresAcrossStores()
    {
        DataStore storeA = new();
        Uuid uuid = storeA.GetUuid(storeA.Alloc());

        DataStore storeB = new();
        int entity = storeB.Alloc(uuid);

        Assert.Equal(uuid, storeB.GetUuid(entity));
        Assert.True(storeB.TryGet(uuid, out int resolved));
        Assert.Equal(entity, resolved);
    }

    [Fact]
    public void AllocProducesSeedCounterUuids()
    {
        DataStore store = new();
        int first = store.Alloc();
        int second = store.Alloc();

        ulong firstValue = store.GetUuid(first).ToValue();
        ulong secondValue = store.GetUuid(second).ToValue();

        Assert.NotEqual(firstValue, Uuid.Null.ToValue());
        Assert.Equal(firstValue >> 32, secondValue >> 32);
        Assert.Equal(firstValue + 1, secondValue);
    }

    [Fact]
    public void DataStoreNewUuidIsUnique()
    {
        DataStore store = new();

        Assert.NotEqual(store.NewUuid(), Uuid.Null);
        Assert.NotEqual(store.NewUuid(), store.NewUuid());
    }

    [Fact]
    public void AllocUuidNullThrows()
    {
        DataStore store = new();

        Assert.Throws<InvalidOperationException>(() => store.Alloc(Uuid.Null));
    }
}
