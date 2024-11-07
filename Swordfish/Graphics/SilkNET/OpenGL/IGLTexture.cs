using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal interface IGLTexture
{
    string Name { get; }

    void Activate(TextureUnit textureSlot = TextureUnit.Texture0);
}

internal interface IGLTexture<T> : IGLTexture, IEquatable<T>;
