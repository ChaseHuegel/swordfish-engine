using System.Numerics;
using System.Runtime.InteropServices;

namespace Swordfish.Graphics.SilkNET.OpenGL.Pipelines;

[StructLayout(LayoutKind.Sequential, Pack = 16)]
internal struct GPULight 
{
    public Vector4 PosRadius;       // x,y,z,r
    public Vector4 ColorIntensity;  // r,g,b,intensity
    
    public GPULight(Vector4 posRadius, Vector4 colorIntensity)
    {
        PosRadius = posRadius;
        ColorIntensity = colorIntensity;
    }
}