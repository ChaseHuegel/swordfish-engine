using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLMaterial : Handle
{
    public ShaderProgram ShaderProgram { get; }

    public TexImage2D[] Textures { get; }

    public GLMaterial(ShaderProgram shaderProgram, params TexImage2D[] textures)
    {
        ShaderProgram = shaderProgram;
        Textures = textures;

        if (textures.Length > 32)
            throw new ArgumentException("Length can not exceed 32.", nameof(textures));
    }

    protected override void OnDisposed()
    {
        //  We do not want to dispose the shader and textures, they may be shared with other materials.
    }

    public void Use()
    {
        if (disposed)
        {
            Debugger.Log($"Attempted to use {this} but it is disposed.", LogType.ERROR);
            return;
        }

        ShaderProgram.Use();

        for (int i = 0; i < Textures.Length; i++)
        {
            Textures[i].Bind(TextureUnit.Texture0 + i);
            ShaderProgram.SetUniform("texture" + i, i);
        }
    }
}