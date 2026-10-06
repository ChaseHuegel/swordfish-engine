using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Shared.Networking.Components;
using NUnit.Framework;

namespace WaywardBeyond.Client.Core.Tests;

/// <summary>
/// The tag-based client teardown frees every "game" entity. That now covers the local player, the remote
/// player mirrors, and the world geometry, so a remote mirror can no longer render over the menu after
/// save and quit. Entities on other tags (the persistent audio channels) survive.
/// </summary>
public class ClientCleanupSystemTests
{
    [Test]
    public void CleanupFreesEveryGameEntityAndKeepsOtherTags()
    {
        var store = new DataStore();
        var cleanup = new ClientCleanupSystem(NullLogger<ClientCleanupSystem>.Instance);

        int localPlayer = store.Alloc();
        store.AddOrUpdate(localPlayer, new IdentifierComponent("Local", "game"));
        store.AddOrUpdate(localPlayer, new PlayerComponent());

        int remotePlayer = store.Alloc();
        store.AddOrUpdate(remotePlayer, new IdentifierComponent("Remote", "game"));
        store.AddOrUpdate(remotePlayer, new BodyViewComponent { Body = "wb:m_human" });

        int worldEntity = store.Alloc();
        store.AddOrUpdate(worldEntity, new IdentifierComponent(null, "game"));

        int audioEntity = store.Alloc();
        store.AddOrUpdate(audioEntity, new IdentifierComponent("master", "audio"));

        cleanup.RequestCleanup();
        cleanup.Tick(0f, store);

        Assert.That(store.TryGet(localPlayer, out IdentifierComponent _), Is.False, "The local player must be freed.");
        Assert.That(store.TryGet(remotePlayer, out IdentifierComponent _), Is.False, "The remote mirror must be freed.");
        Assert.That(store.TryGet(remotePlayer, out BodyViewComponent _), Is.False, "The remote mirror's body view must be freed.");
        Assert.That(store.TryGet(worldEntity, out IdentifierComponent _), Is.False, "The world entity must be freed.");
        Assert.That(store.TryGet(audioEntity, out IdentifierComponent _), Is.True, "A non-game entity must survive teardown.");
    }

    [Test]
    public void CleanupDoesNothingUntilRequested()
    {
        var store = new DataStore();
        var cleanup = new ClientCleanupSystem(NullLogger<ClientCleanupSystem>.Instance);

        int entity = store.Alloc();
        store.AddOrUpdate(entity, new IdentifierComponent("world", "game"));

        cleanup.Tick(0f, store);

        Assert.That(store.TryGet(entity, out IdentifierComponent _), Is.True, "Teardown must wait for a request.");
    }
}
