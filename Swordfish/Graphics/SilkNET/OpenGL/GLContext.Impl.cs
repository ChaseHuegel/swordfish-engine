using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal unsafe partial class GLContext
{
    private readonly struct SharderProgramArgs
    {
        private readonly GL gl;
        private readonly string name;
        private readonly ShaderComponent[] shaderComponents;

        public SharderProgramArgs(GL gl, string name, ShaderComponent[] shaderComponents)
        {
            this.gl = gl;
            this.name = name;
            this.shaderComponents = shaderComponents;
        }

        public static ShaderProgram Factory(SharderProgramArgs args)
        {
            return new ShaderProgram(args.gl, args.name, args.shaderComponents);
        }
    }

    private readonly struct SharderComponentArgs
    {
        private readonly GL gl;
        private readonly string name;
        private readonly Silk.NET.OpenGL.ShaderType type;
        private readonly string source;

        public SharderComponentArgs(GL gl, string name, Silk.NET.OpenGL.ShaderType type, string source)
        {
            this.gl = gl;
            this.name = name;
            this.type = type;
            this.source = source;
        }

        public static ShaderComponent Factory(SharderComponentArgs args)
        {
            return new ShaderComponent(args.gl, args.name, args.type, args.source);
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

    private readonly struct TextureArrayArgs
    {
        private readonly GL gl;
        private readonly string name;
        private readonly byte* pixels;
        private readonly uint width;
        private readonly uint height;
        private readonly uint depth;
        private readonly bool generateMipmaps;

        public TextureArrayArgs(GL gl, string name, byte* pixels, uint width, uint height, uint depth, bool generateMipmaps)
        {
            this.gl = gl;
            this.name = name;
            this.width = width;
            this.height = height;
            this.depth = depth;
            this.generateMipmaps = generateMipmaps;
            this.pixels = pixels;
        }

        public static TexImage3D Factory(TextureArrayArgs args)
        {
            return new TexImage3D(args.gl, args.name, args.pixels, args.width, args.height, args.depth, args.generateMipmaps);
        }
    }

    private readonly struct GLMaterialArgs
    {
        private readonly ShaderProgram shaderProgram;
        private readonly IGLTexture[] textures;

        public GLMaterialArgs(ShaderProgram shaderProgram, IGLTexture[] texImages2D)
        {
            this.shaderProgram = shaderProgram;
            this.textures = texImages2D;
        }

        public static GLMaterial Factory(GLMaterialArgs args)
        {
            return new GLMaterial(args.shaderProgram, args.textures);
        }
    }

    private readonly struct GLRenderTargetArgs
    {
        private readonly GL gl;
        private readonly Transform transform;
        private readonly VertexArrayObject<float, uint> vertexArrayObject;
        private readonly BufferObject<Matrix4x4> modelsBufferObject;
        private readonly GLMaterial[] materials;
        private readonly RenderOptions renderOptions;

        public GLRenderTargetArgs(GL gl, Transform transform, VertexArrayObject<float, uint> vertexArrayObject, BufferObject<Matrix4x4> modelsBufferObject, GLMaterial[] materials, RenderOptions renderOptions)
        {
            this.gl = gl;
            this.transform = transform;
            this.vertexArrayObject = vertexArrayObject;
            this.modelsBufferObject = modelsBufferObject;
            this.materials = materials;
            this.renderOptions = renderOptions;
        }

        public static GLRenderTarget Factory(GLRenderTargetArgs args)
        {
            return new GLRenderTarget(args.gl, args.transform, args.vertexArrayObject, args.modelsBufferObject, args.materials, args.renderOptions);
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

    private readonly struct BufferObjectArgs<TData> where TData : unmanaged
    {
        private readonly GL gl;
        private readonly TData[] data;
        private readonly BufferTargetARB bufferType;
        private readonly BufferUsageARB usage;

        public BufferObjectArgs(GL gl, TData[] data, BufferTargetARB bufferType, BufferUsageARB usage)
        {
            this.gl = gl;
            this.data = data;
            this.bufferType = bufferType;
            this.usage = usage;
        }

        public static BufferObject<TData> Factory(BufferObjectArgs<TData> args)
        {
            return new BufferObject<TData>(args.gl, args.data, args.bufferType, args.usage);
        }
    }
}
