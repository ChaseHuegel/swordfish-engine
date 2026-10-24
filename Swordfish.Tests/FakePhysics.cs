#nullable enable
using System;
using System.Numerics;
using Swordfish.Physics;

namespace Swordfish.Tests;

/// <summary>A physics stub for tests that read the fixed timestep but never step a solver.</summary>
internal sealed class FakePhysics : IPhysics
{
    /// <summary>The fixed timestep the shared step reads; 64 Hz matches the default physics config.</summary>
    public float FixedTimestamp => 1f / 64f;

    public event EventHandler<EventArgs>? FixedUpdate
    {
        add { }
        remove { }
    }

    public RaycastResult Raycast(in Ray ray) => default;

    public void SetGravity(Vector3 gravity) { }
}
