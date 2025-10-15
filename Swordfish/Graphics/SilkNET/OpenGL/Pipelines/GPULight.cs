using System.Numerics;
using System.Runtime.InteropServices;

namespace Swordfish.Graphics.SilkNET.OpenGL.Pipelines;

[StructLayout(LayoutKind.Sequential)]
internal struct GPULight 
{
    public Vector4 PosRadius;       // x,y,z,r
    public Vector4 ColorIntensity;  // r,g,b,intensity
}