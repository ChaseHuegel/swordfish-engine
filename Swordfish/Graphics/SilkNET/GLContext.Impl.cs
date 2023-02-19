using Silk.NET.OpenGL;
using Swordfish.Library.Extensions;

namespace Swordfish.Graphics.SilkNET;

internal unsafe partial class GLContext
{
    private readonly struct SharderProgramArgs
    {
        private readonly GL gl;
        private readonly string name;
        private readonly string vertexSource;
        private readonly string fragmentSource;

        public SharderProgramArgs(GL gl, string name, string vertexSource, string fragmentSource)
        {
            this.gl = gl;
            this.name = name;
            this.vertexSource = vertexSource;
            this.fragmentSource = fragmentSource;
        }

        public static ShaderProgram Create(SharderProgramArgs args)
        {
            return new ShaderProgram(args.gl, args.name, args.vertexSource, args.fragmentSource);
        }
    }

    private readonly struct TextureArgs
    {
        private readonly GL gl;
        private readonly string name;
        private readonly byte* pixels;
        private readonly uint width;
        private readonly uint height;
        private readonly bool generateMipmaps;

        public TextureArgs(GL gl, string name, byte* pixels, uint width, uint height, bool generateMipmaps)
        {
            this.gl = gl;
            this.name = name;
            this.width = width;
            this.height = height;
            this.generateMipmaps = generateMipmaps;
            this.pixels = pixels;
        }

        public static TexImage2D Create(TextureArgs args)
        {
            return new TexImage2D(args.gl, args.name, args.pixels, args.width, args.height, args.generateMipmaps);
        }
    }
}
