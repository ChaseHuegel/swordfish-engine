using System.Numerics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Renderers;
using Swordfish.Library.Collections;

namespace Swordfish.Graphics;

internal record struct RenderScene(
    in Matrix4x4 View,
    in Matrix4x4 Projection,
    in EntityModel[] EntityModels,
    in LockedList<GLRenderTarget> RenderTargets,
    in LockedList<GLRectRenderTarget> RectRenderTargets
);