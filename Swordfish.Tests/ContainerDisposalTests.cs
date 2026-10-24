#nullable enable
using System;
using DryIoc;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Pins the container behaviour the server world relies on: a world is an exclusive child of the host
/// container, and disposing it must dispose the instances the world pinned while leaving host-owned
/// singletons alive.
/// </summary>
public class ContainerDisposalTests
{
    [Fact]
    public void WorldChildContainerDisposesPinnedInstances()
    {
        using var root = new Container();

        IContainer child = CreateWorldContainer(root);
        var pinned = new TrackedDisposable();
        child.RegisterInstance(pinned);

        child.Dispose();

        Assert.True(pinned.Disposed, "A disposed world container must dispose the instances it pinned.");
    }

    [Fact]
    public void WorldChildContainerDoesNotDisposeRootInstances()
    {
        using var root = new Container();
        var shared = new RootDisposable();
        root.RegisterInstance(shared);

        IContainer child = CreateWorldContainer(root);
        child.Resolve<RootDisposable>();

        child.Dispose();

        Assert.False(shared.Disposed, "Disposing a world container must not dispose a host-owned instance.");
    }

    private static IContainer CreateWorldContainer(IContainer root)
    {
        //  Mirrors ServerWorldHost: a world must not cascade disposal into the host's singletons.
        return root.CreateChild(RegistrySharing.CloneAndDropCache, null, null, null, withDisposables: false);
    }

    private sealed class TrackedDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class RootDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
