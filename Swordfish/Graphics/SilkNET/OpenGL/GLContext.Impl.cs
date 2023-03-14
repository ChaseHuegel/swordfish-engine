using Silk.NET.OpenGL;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

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

        public static ShaderProgram Factory(SharderProgramArgs args)
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

        public static TexImage2D Factory(TextureArgs args)
        {
            return new TexImage2D(args.gl, args.name, args.pixels, args.width, args.height, args.generateMipmaps);
        }
    }

    private readonly struct GLMaterialArgs
    {
        private readonly ShaderProgram shaderProgram;
        private readonly TexImage2D[] texImages2D;

        public GLMaterialArgs(ShaderProgram shaderProgram, TexImage2D[] texImages2D)
        {
            this.shaderProgram = shaderProgram;
            this.texImages2D = texImages2D;
        }

        public static GLMaterial Factory(GLMaterialArgs args)
        {
            return new GLMaterial(args.shaderProgram, args.texImages2D);
        }
    }

    private readonly struct GLRenderTargetArgs
    {
        private readonly GL gl;
        private readonly Transform transform;
        private readonly float[] vertexData;
        private readonly uint[] indices;
        private readonly GLMaterial[] materials;

        public GLRenderTargetArgs(GL gl, Transform transform, float[] vertexData, uint[] indices, GLMaterial[] materials)
        {
            this.gl = gl;
            this.transform = transform;
            this.vertexData = vertexData;
            this.indices = indices;
            this.materials = materials;
        }

        public static GLRenderTarget Factory(GLRenderTargetArgs args)
        {
            return new GLRenderTarget(args.gl, args.transform, args.vertexData, args.indices, args.materials);
        }
    }

    private readonly struct VertexArrayObjectArgs<TVertexType, TElementType>
        where TVertexType : unmanaged
        where TElementType : unmanaged
    {
        private readonly GL gl;
        private readonly TVertexType[] vertexData;
        private readonly TElementType[] indices;

        public VertexArrayObjectArgs(GL gl, TVertexType[] vertexData, TElementType[] indices)
        {
            this.gl = gl;
            this.vertexData = vertexData;
            this.indices = indices;
        }

        public static VertexArrayObject<TVertexType, TElementType> Factory(VertexArrayObjectArgs<TVertexType, TElementType> args)
        {
            var vertexBufferObject = new BufferObject<TVertexType>(args.gl, args.vertexData, BufferTargetARB.ArrayBuffer);
            var elementBufferObject = new BufferObject<TElementType>(args.gl, args.indices, BufferTargetARB.ElementArrayBuffer);
            return new VertexArrayObject<TVertexType, TElementType>(args.gl, vertexBufferObject, elementBufferObject);
        }
    }

    private readonly struct VertexArrayObject32Args
    {
        private readonly GL gl;
        private readonly float[] vertexData;
        private readonly uint[] indices;

        public VertexArrayObject32Args(GL gl, float[] vertexData, uint[] indices)
        {
            this.gl = gl;
            this.vertexData = vertexData;
            this.indices = indices;
        }

        public static VertexArrayObject32 Factory(VertexArrayObject32Args args)
        {
            var vertexBufferObject = new BufferObject<float>(args.gl, args.vertexData, BufferTargetARB.ArrayBuffer);
            var elementBufferObject = new BufferObject<uint>(args.gl, args.indices, BufferTargetARB.ElementArrayBuffer);
            return new VertexArrayObject32(args.gl, vertexBufferObject, elementBufferObject);
        }
    }
}
