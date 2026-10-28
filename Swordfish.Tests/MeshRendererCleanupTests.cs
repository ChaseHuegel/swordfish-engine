using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Systems;
using Xunit;

namespace Swordfish.Tests;

public class MeshRendererCleanupTests
{
    [Fact]
    public void CleanupTickDisposesQueuedRenderersAndMeshes()
    {
        DataStore store = new();
        Mesh opaqueMesh = new([], [], [], [], []);
        Mesh transparentMesh = new([], [], [], [], []);
        var opaqueRenderer = new MeshRenderer(opaqueMesh);
        var transparentRenderer = new MeshRenderer(transparentMesh);
        var cleanup = new MeshRendererCleanup();
        cleanup.MeshRenderers.Add(opaqueRenderer);
        cleanup.MeshRenderers.Add(transparentRenderer);
        store.AddOrUpdate(store.Alloc(), cleanup);

        var system = new CleanupMeshRendererSystem();
        system.Tick(0f, store);

        Assert.True(opaqueRenderer.IsDisposed);
        Assert.True(transparentRenderer.IsDisposed);
        Assert.True(opaqueMesh.IsDisposed);
        Assert.True(transparentMesh.IsDisposed);
        Assert.True(cleanup.MeshRenderers.IsEmpty);
    }

    [Fact]
    public void ClientCleanupDrainsQueuedRenderersBeforeFreeing()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new IdentifierComponent(name: null, tag: "game"));

        Mesh queuedMesh = new([], [], [], [], []);
        var queuedRenderer = new MeshRenderer(queuedMesh);
        var cleanup = new MeshRendererCleanup();
        cleanup.MeshRenderers.Add(queuedRenderer);
        store.AddOrUpdate(entity, cleanup);

        Mesh currentMesh = new([], [], [], [], []);
        var currentRenderer = new MeshRenderer(currentMesh);
        store.AddOrUpdate(entity, new MeshRendererComponent(currentRenderer));

        var system = new ClientCleanupSystem(NullLogger<ClientCleanupSystem>.Instance);
        system.RequestCleanup();
        system.Tick(0f, store);

        Assert.True(queuedRenderer.IsDisposed);
        Assert.True(queuedMesh.IsDisposed);
        Assert.True(currentRenderer.IsDisposed);
        Assert.True(currentMesh.IsDisposed);
        Assert.False(store.Has(typeof(MeshRendererCleanup), entity));
    }

    [Fact]
    public void PendingReplacementRetainedUntilNewRendererIsBound()
    {
        DataStore store = new();
        Mesh oldMesh = new([], [], [], [], []);
        Mesh newMesh = new([], [], [], [], []);
        var oldRenderer = new MeshRenderer(oldMesh);
        var newRenderer = new MeshRenderer(newMesh);
        var cleanup = new MeshRendererCleanup();
        cleanup.Pending.Enqueue(new MeshRendererCleanup.PendingEntry(oldRenderer, newRenderer));
        store.AddOrUpdate(store.Alloc(), cleanup);

        var system = new CleanupMeshRendererSystem();
        system.Tick(0f, store);

        Assert.False(oldRenderer.IsDisposed);
        Assert.False(oldMesh.IsDisposed);

        newRenderer.IsBound = true;
        system.Tick(0f, store);

        Assert.True(oldRenderer.IsDisposed);
        Assert.True(oldMesh.IsDisposed);
        Assert.True(cleanup.Pending.IsEmpty);
    }

    [Fact]
    public void PendingReplacementDisposedAfterAgeThresholdWhenNeverBound()
    {
        DataStore store = new();
        Mesh oldMesh = new([], [], [], [], []);
        Mesh newMesh = new([], [], [], [], []);
        var oldRenderer = new MeshRenderer(oldMesh);
        var newRenderer = new MeshRenderer(newMesh);
        var cleanup = new MeshRendererCleanup();
        cleanup.Pending.Enqueue(new MeshRendererCleanup.PendingEntry(oldRenderer, newRenderer));
        store.AddOrUpdate(store.Alloc(), cleanup);

        var system = new CleanupMeshRendererSystem();
        for (var i = 0; i < 4; i++)
        {
            system.Tick(0f, store);
            Assert.False(oldRenderer.IsDisposed);
        }

        system.Tick(0f, store);
        Assert.True(oldRenderer.IsDisposed);
        Assert.True(oldMesh.IsDisposed);
        Assert.True(cleanup.Pending.IsEmpty);
    }
}
