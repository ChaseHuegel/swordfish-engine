using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Library.Types;
using Swordfish.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal unsafe partial class GLContext
{
    private readonly struct ShaderProgramArgs(
        in GL gl,
        in string name,
        in ShaderComponent[] shaderComponents)
    {
        private readonly GL _gl = gl;
        private readonly string _name = name;
        private readonly ShaderComponent[] _shaderComponents = shaderComponents;

        public static ShaderProgram Factory(ShaderProgramArgs args)
        {
            return new ShaderProgram(args._gl, args._name, args._shaderComponents);
        }
    }

    private readonly struct ShaderComponentArgs(
        in GL gl,
        in string name,
        in Silk.NET.OpenGL.ShaderType type,
        in string source)
    {
        private readonly GL _gl = gl;
        private readonly string _name = name;
        private readonly Silk.NET.OpenGL.ShaderType _type = type;
        private readonly string _source = source;

        public static ShaderComponent Factory(ShaderComponentArgs args)
        {
            return new ShaderComponent(args._gl, args._name, args._type, args._source);
        }
    }

    private readonly struct TextureArgs(
        in GL gl,
        in string name,
        in byte* pixels,
        in uint width,
        in uint height,
        in TextureFormat format,
        in TextureParams @params)
    {
        private readonly GL _gl = gl;
        private readonly string _name = name;
        private readonly byte* _pixels = pixels;
        private readonly uint _width = width;
        private readonly uint _height = height;
        private readonly TextureFormat _format = format;
        private readonly TextureParams _params = @params;

        public static TexImage2D Factory(TextureArgs args)
        {
            return new TexImage2D(args._gl, args._name, args._pixels, args._width, args._height, args._format, args._params);
        }
    }

    private readonly struct TextureArrayArgs(
        in GL gl,
        in string name,
        in byte* pixels,
        in uint width,
        in uint height,
        in uint depth,
        in TextureFormat format,
        in TextureParams @params)
    {
        private readonly GL _gl = gl;
        private readonly string _name = name;
        private readonly byte* _pixels = pixels;
        private readonly uint _width = width;
        private readonly uint _height = height;
        private readonly uint _depth = depth;
        private readonly TextureFormat _format = format;
        private readonly TextureParams _params = @params;

        public static TexImage3D Factory(TextureArrayArgs args)
        {
            return new TexImage3D(args._gl, args._name, args._pixels, args._width, args._height, args._depth, args._format, args._params);
        }
    }

    private readonly struct GLMaterialArgs(
        in ShaderProgram shaderProgram, 
        in IGLTexture[] texImages2D, 
        in bool transparent)
    {
        private readonly ShaderProgram _shaderProgram = shaderProgram;
        private readonly IGLTexture[] _textures = texImages2D;
        private readonly bool _transparent = transparent;

        public static GLMaterial Factory(GLMaterialArgs args)
        {
            return new GLMaterial(args._shaderProgram, args._textures, args._transparent);
        }
    }

    private readonly struct GLRenderTargetArgs(
        in GL gl,
        in Transform transform,
        in VertexArrayObject<float, uint> vertexArrayObject,
        in BufferObject<Matrix4x4> modelsBufferObject,
        in GLMaterial[] materials,
        in RenderOptions renderOptions)
    {
        private readonly GL _gl = gl;
        private readonly Transform _transform = transform;
        private readonly VertexArrayObject<float, uint> _vertexArrayObject = vertexArrayObject;
        private readonly BufferObject<Matrix4x4> _modelsBufferObject = modelsBufferObject;
        private readonly GLMaterial[] _materials = materials;
        private readonly RenderOptions _renderOptions = renderOptions;

        public static GLRenderTarget Factory(GLRenderTargetArgs args)
        {
            return new GLRenderTarget(args._gl, args._transform, args._vertexArrayObject, args._modelsBufferObject, args._materials, args._renderOptions);
        }
    }
    
    private readonly struct GLRectRenderTargetArgs(
        in Rect2 rect,
        in Vector4 color,
        in GLMaterial[] materials)
    {
        private readonly Rect2 _rect = rect;
        private readonly Vector4 _color = color;
        private readonly GLMaterial[] _materials = materials;

        public static GLRectRenderTarget Factory(GLRectRenderTargetArgs args)
        {
            return new GLRectRenderTarget(args._rect, args._color, args._materials);
        }
    }

    private readonly struct VertexArrayObjectArgs<TVertexType>(
        in GL gl, 
        in TVertexType[] vertexData)
        where TVertexType : unmanaged
    {
        private readonly GL _gl = gl;
        private readonly TVertexType[] _vertexData = vertexData;

        public static VertexArrayObject<TVertexType> Factory(VertexArrayObjectArgs<TVertexType> args)
        {
            var vertexBufferObject = new BufferObject<TVertexType>(args._gl, args._vertexData, BufferTargetARB.ArrayBuffer);
            return new VertexArrayObject<TVertexType>(args._gl, vertexBufferObject);
        }
    }

    private readonly struct VertexArrayObjectArgs<TVertexType, TElementType>(
        in GL gl,
        in TVertexType[] vertexData,
        in TElementType[] indices)
        where TVertexType : unmanaged
        where TElementType : unmanaged
    {
        private readonly GL _gl = gl;
        private readonly TVertexType[] _vertexData = vertexData;
        private readonly TElementType[] _indices = indices;

        public static VertexArrayObject<TVertexType, TElementType> Factory(VertexArrayObjectArgs<TVertexType, TElementType> args)
        {
            var vertexBufferObject = new BufferObject<TVertexType>(args._gl, args._vertexData, BufferTargetARB.ArrayBuffer);
            var elementBufferObject = new BufferObject<TElementType>(args._gl, args._indices, BufferTargetARB.ElementArrayBuffer);
            return new VertexArrayObject<TVertexType, TElementType>(args._gl, vertexBufferObject, elementBufferObject);
        }
    }

    private readonly struct VertexArrayObject32Args(
        in GL gl, 
        in float[] vertexData, 
        in uint[] indices)
    {
        private readonly GL _gl = gl;
        private readonly float[] _vertexData = vertexData;
        private readonly uint[] _indices = indices;

        public static VertexArrayObject32 Factory(VertexArrayObject32Args args)
        {
            var vertexBufferObject = new BufferObject<float>(args._gl, args._vertexData, BufferTargetARB.ArrayBuffer);
            var elementBufferObject = new BufferObject<uint>(args._gl, args._indices, BufferTargetARB.ElementArrayBuffer);
            return new VertexArrayObject32(args._gl, vertexBufferObject, elementBufferObject);
        }
    }

    private readonly struct BufferObjectArgs<TData>(
        in GL gl,
        in TData[] data,
        in BufferTargetARB bufferType,
        in BufferUsageARB usage)
        where TData : unmanaged
    {
        private readonly GL _gl = gl;
        private readonly TData[] _data = data;
        private readonly BufferTargetARB _bufferType = bufferType;
        private readonly BufferUsageARB _usage = usage;

        public static BufferObject<TData> Factory(BufferObjectArgs<TData> args)
        {
            return new BufferObject<TData>(args._gl, args._data, args._bufferType, args._usage);
        }
    }
}
