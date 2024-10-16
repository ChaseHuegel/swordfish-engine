using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.IO;
using Shader = Swordfish.Graphics.Shader;

internal class GLLineRenderer : IRenderStage, ILineRenderer
{
    private ShaderProgram? ShaderProgram;
    private VertexArrayObject<float>? VAO;

    //  ! There will likely be lock contention issues later.
    private readonly object LinesLock = new();

    private readonly List<Line> Lines = [];
    private readonly List<int> LineVertexOffsets = [];
    private readonly List<uint> LineVertexCounts = [];
    private readonly List<float> LineVertexData = [];

    private readonly GL GL;
    private readonly GLContext GLContext;
    private readonly IFileService FileService;
    private readonly IPathService PathService;

    public GLLineRenderer(GL gl, GLContext glContext, IFileService fileService, IPathService pathService)
    {
        GL = gl;
        GLContext = glContext;
        FileService = fileService;
        PathService = pathService;
    }

    public void Load(IRenderContext renderContext)
    {
        Shader shader = FileService.Parse<Shader>(PathService.Shaders.At("lines.glsl"));
        VAO = GLContext.CreateVertexArrayObject(Array.Empty<float>());

        VAO.Bind();
        VAO.VertexBufferObject.Bind();
        VAO.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, 7, 0);
        VAO.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, 7, 3);

        ShaderProgram = ShaderToShaderProgram(shader);
        ShaderProgram.BindAttributeLocation("in_position", 0);
        ShaderProgram.BindAttributeLocation("in_color", 1);

        GL.Enable(GLEnum.LineSmooth);
    }

    public void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
    }

    public unsafe int Render(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        lock (LinesLock)
        {
            ShaderProgram!.Activate();
            ShaderProgram.SetUniform("view", view);
            ShaderProgram.SetUniform("projection", projection);

            VAO!.Bind();

            for (int i = 0, n = 0; i < Lines.Count; i++, n += 14)
            {
                Line line = Lines[i];
                LineVertexData[n + 0] = line.Start.X;
                LineVertexData[n + 1] = line.Start.Y;
                LineVertexData[n + 2] = line.Start.Z;

                LineVertexData[n + 3] = line.Color.X;
                LineVertexData[n + 4] = line.Color.Y;
                LineVertexData[n + 5] = line.Color.Z;
                LineVertexData[n + 6] = line.Color.W;

                LineVertexData[n + 7] = line.End.X;
                LineVertexData[n + 8] = line.End.Y;
                LineVertexData[n + 9] = line.End.Z;

                LineVertexData[n + 10] = line.Color.X;
                LineVertexData[n + 11] = line.Color.Y;
                LineVertexData[n + 12] = line.Color.Z;
                LineVertexData[n + 13] = line.Color.W;
            }

            VAO.VertexBufferObject.UpdateData(CollectionsMarshal.AsSpan(LineVertexData));
            GL.MultiDrawArrays(PrimitiveType.Lines, CollectionsMarshal.AsSpan(LineVertexOffsets), CollectionsMarshal.AsSpan(LineVertexCounts), (uint)Lines.Count);
        }

        return 1;
    }

    private ShaderProgram ShaderToShaderProgram(Shader shader)
    {
        ShaderComponent[] shaderComponents = shader.Sources.Select(ShaderSourceToShaderComponent).ToArray();
        return GLContext.CreateShaderProgram(shader.Name, shaderComponents);
    }

    private ShaderComponent ShaderSourceToShaderComponent(ShaderSource shaderSource)
    {
        return GLContext.CreateShaderComponent(shaderSource.Name, shaderSource.Type.ToSilkShaderType(), shaderSource.Source);
    }

    public Line CreateLine()
    {
        return CreateLine(Vector3.Zero, Vector3.Zero);
    }

    public Line CreateLine(Vector3 start, Vector3 end)
    {
        return CreateLine(start, end, Vector4.One);
    }

    public Line CreateLine(Vector3 start, Vector3 end, Vector4 color)
    {
        var line = new Line(this, start, end, color);
        lock (LinesLock)
        {
            LineVertexOffsets.Add(Lines.Count * 2);
            LineVertexCounts.Add(2);

            LineVertexData.Capacity += 14;

            // start X,Y,Z
            LineVertexData.Add(0);
            LineVertexData.Add(0);
            LineVertexData.Add(0);

            // start r,g,b,a
            LineVertexData.Add(0);
            LineVertexData.Add(0);
            LineVertexData.Add(0);
            LineVertexData.Add(0);

            // end X,Y,Z
            LineVertexData.Add(0);
            LineVertexData.Add(0);
            LineVertexData.Add(0);

            // end r,g,b,a
            LineVertexData.Add(0);
            LineVertexData.Add(0);
            LineVertexData.Add(0);
            LineVertexData.Add(0);

            Lines.Add(line);
        }

        return line;
    }

    public void DeleteLine(Line line)
    {
        lock (LinesLock)
        {
            int index = Lines.IndexOf(line);
            LineVertexOffsets.RemoveAt(index);
            LineVertexCounts.RemoveAt(index);
            LineVertexData.RemoveRange(index, 14);
            Lines.RemoveAt(index);
        }
    }
}