using System.Numerics;
using System.Runtime.InteropServices;

namespace Swordfish.Graphics.SilkNET.OpenGL.Pipelines;

[StructLayout(LayoutKind.Sequential, Pack = 16)]
internal struct GPULight(Vector4 posRadius, Vector4 colorSize, Vector4 shadowParams = default)
{
    public Vector4 PosRadius = posRadius;  // x,y,z,r
    public Vector4 ColorSize = colorSize;  // r,g,b,size
    public Vector4 ShadowParams = shadowParams; // shadowIndex, bias, reserved, reserved
}