using System.Numerics;
using System.Runtime.InteropServices;

namespace Swordfish.Graphics.Particles;

/// <summary>Per-particle GPU instance payload uploaded to the vertex stream.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ParticleInstance
{
    public Vector3 Position;
    public float Size;
    public Vector4 Color;
    public float Rotation;
    public float Emissive;
}
