using System.Numerics;
using System.Runtime.InteropServices;

namespace Swordfish.Graphics.SilkNET.OpenGL.Pipelines;

[StructLayout(LayoutKind.Sequential, Pack = 16)]
internal struct GPULight 
{
    public Vector4 PosRadius;  // x,y,z,r
    public Vector4 ColorSize;  // r,g,b,size
    
    public GPULight(Vector4 posRadius, Vector4 colorSize)
    {
        PosRadius = posRadius;
        ColorSize = colorSize;
    }
}