using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using Swordfish.Settings;
using Swordfish.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL.Renderers;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class GLScreenSpaceRenderer(in GL gl, in GLContext glContext, in RenderSettings renderSettings) : IScreenSpaceRenderStage
{
    private readonly struct RectVertices()
    {
        public readonly List<int> Offsets = [];
        public readonly List<uint> Counts = [];
        public readonly List<float> Data = [];

        public int Count => Counts.Count;

        public void AddVertexData(Rect2 rect, Vector4 color)
        {
            Offsets.Add(Count * 4);
            Counts.Add(4);

            Data.Capacity += 20;

            //  Bottom left
            // X,Y,Z
            Data.Add(MathS.RangeToRange(rect.Min.X, 0f, 1f, -1f, 1f));
            Data.Add(MathS.RangeToRange(rect.Min.Y, 0f, 1f, -1f, 1f));
            Data.Add(0f);
            //  r,g,b,a
            Data.Add(color.X);
            Data.Add(color.Y);
            Data.Add(color.Z);
            Data.Add(color.W);
            // u,v
            Data.Add(0);
            Data.Add(1);
            Data.Add(0);
            
            //  Bottom right
            // X,Y,Z
            Data.Add(MathS.RangeToRange(rect.Max.X, 0f, 1f, -1f, 1f));
            Data.Add(MathS.RangeToRange(rect.Min.Y, 0f, 1f, -1f, 1f));
            Data.Add(0f);
            //  r,g,b,a
            Data.Add(color.X);
            Data.Add(color.Y);
            Data.Add(color.Z);
            Data.Add(color.W);
            // u,v
            Data.Add(1);
            Data.Add(1);
            Data.Add(0);
            
            //  Top right
            // X,Y,Z
            Data.Add(MathS.RangeToRange(rect.Max.X, 0f, 1f, -1f, 1f));
            Data.Add(MathS.RangeToRange(rect.Max.Y, 0f, 1f, -1f, 1f));
            Data.Add(0f);
            //  r,g,b,a
            Data.Add(color.X);
            Data.Add(color.Y);
            Data.Add(color.Z);
            Data.Add(color.W);
            // u,v
            Data.Add(1);
            Data.Add(0);
            Data.Add(0);
  
            //  Top left
            // X,Y,Z
            Data.Add(MathS.RangeToRange(rect.Min.X, 0f, 1f, -1f, 1f));
            Data.Add(MathS.RangeToRange(rect.Max.Y, 0f, 1f, -1f, 1f));
            Data.Add(0f);
            //  r,g,b,a
            Data.Add(color.X);
            Data.Add(color.Y);
            Data.Add(color.Z);
            Data.Add(color.W);
            // u,v
            Data.Add(0);
            Data.Add(0);
            Data.Add(0);
        }
    }
    
    private readonly GL _gl = gl;
    private readonly GLContext _glContext = glContext;
    private readonly RenderSettings _renderSettings = renderSettings;
    
    private VertexArrayObject<float>? _vao;
    private LockedList<GLRectRenderTarget>? _renderTargets;
    private readonly Dictionary<GLRectRenderTarget, RectVertices> _instances = [];

    public void Initialize(IRenderContext renderContext)
    {
        if (renderContext is not GLRenderContext glRenderContext)
        {
            throw new NotSupportedException($"{nameof(GLScreenSpaceRenderer)} only supports an OpenGL {nameof(IRenderContext)}.");
        }
        
        _vao = _glContext.CreateVertexArrayObject(Array.Empty<float>());

        _vao.Bind();
        _vao.VertexBufferObject.Bind();
        _vao.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, 10, 0);
        _vao.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, 10, 3);
        _vao.SetVertexAttribute(2, 3, VertexAttribPointerType.Float, 10, 7);
        
        //  TODO this is bad
        _renderTargets = glRenderContext.RectRenderTargets;
    }

    public void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection, bool isDepthPass)
    {
        if (isDepthPass)
        {
            return;
        }
        
        if (_renderTargets == null)
        {
            throw new InvalidOperationException($"{nameof(PreRender)} was called without initializing a valid render targets collection.");
        }

        _instances.Clear();
        _renderTargets.ForEach(ForEachRenderTarget);
        return;

        void ForEachRenderTarget(GLRectRenderTarget renderTarget)
        {
            if (!_instances.TryGetValue(renderTarget, out RectVertices vertices))
            {
                vertices = new RectVertices();
                _instances.TryAdd(renderTarget, vertices);
            }
            
            vertices.AddVertexData(renderTarget.Rect, renderTarget.Color);
        }
    }

    public int Render(double delta, Matrix4x4 view, Matrix4x4 projection, Action<ShaderProgram> shaderActivationCallback, bool isDepthPass)
    {
        if (_vao == null || isDepthPass)
        {
            return 0;
        }
        
        if (_renderTargets == null)
        {
            throw new InvalidOperationException($"{nameof(Render)} was called without initializing a valid render targets collection.");
        }

        if (_renderTargets.Count == 0)
        {
            return 0;
        }
        
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var drawCalls = 0;
        _vao.Bind();
        foreach (KeyValuePair<GLRectRenderTarget, RectVertices> instance in _instances)
        {
            drawCalls += Draw(_vao, instance.Key, instance.Value, shaderActivationCallback);
        }
        
        _gl.Disable(EnableCap.Blend);
        return drawCalls;
    }

    private int Draw(VertexArrayObject<float> vao, GLRectRenderTarget target, RectVertices vertices, Action<ShaderProgram> shaderActivationCallback)
    {
        if (vertices.Count == 0)
        {
            return 0;
        }

        GLMaterial.Scope[] materialScopes = ArrayPool<GLMaterial.Scope>.Shared.Rent(target.Materials.Length);
        for (var n = 0; n < target.Materials.Length; n++)
        {
            GLMaterial material = target.Materials[n];
            material.Use();
            shaderActivationCallback(material.ShaderProgram);
        }

        vao.VertexBufferObject.UpdateData(CollectionsMarshal.AsSpan(vertices.Data));
        _gl.Set(EnableCap.DepthTest, false);
        _gl.PolygonMode(TriangleFace.FrontAndBack, _renderSettings.Wireframe ? PolygonMode.Line : PolygonMode.Fill);
        _gl.MultiDrawArrays(PrimitiveType.TriangleFan, CollectionsMarshal.AsSpan(vertices.Offsets), CollectionsMarshal.AsSpan(vertices.Counts), (uint)vertices.Count);
        
        for (var n = 0; n < materialScopes.Length; n++)
        {
            materialScopes[n].Dispose();
        }
        ArrayPool<GLMaterial.Scope>.Shared.Return(materialScopes);
        
        return 1;
    }
}