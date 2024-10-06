using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.IO;
using Shader = Swordfish.Graphics.Shader;

internal class LineRenderer : IRenderStage
{
    private ShaderProgram? ShaderProgram;
    private VertexArrayObject<float>? VAO;

    private readonly List<Line> Lines = new();
    private readonly List<int> LineVertexOffsets = new();
    private readonly List<uint> LineVertexCounts = new();
    private readonly List<float> LineVertexData = new();

    private readonly GL GL;
    private readonly GLContext GLContext;
    private readonly IFileService FileService;
    private readonly IPathService PathService;

    public LineRenderer(GL gl, GLContext glContext, IFileService fileService, IPathService pathService)
    {
        GL = gl;
        GLContext = glContext;
        FileService = fileService;
        PathService = pathService;

        const int GRID_SIZE = 500;

        AddLine(new Line(Vector3.Zero, new Vector3(GRID_SIZE, 0, 0), new Vector4(1f, 0f, 0f, 1f)));
        AddLine(new Line(Vector3.Zero, new Vector3(-GRID_SIZE, 0, 0), new Vector4(1f, 0f, 0f, 0.25f)));

        AddLine(new Line(Vector3.Zero, new Vector3(0, GRID_SIZE, 0), new Vector4(0f, 1f, 0f, 1f)));
        AddLine(new Line(Vector3.Zero, new Vector3(0, -GRID_SIZE, 0), new Vector4(0f, 1f, 0f, 0.25f)));

        AddLine(new Line(Vector3.Zero, new Vector3(0, 0, GRID_SIZE), new Vector4(0f, 0f, 1f, 1f)));
        AddLine(new Line(Vector3.Zero, new Vector3(0, 0, -GRID_SIZE), new Vector4(0f, 0f, 1f, 0.25f)));

        for (int x = -GRID_SIZE; x <= GRID_SIZE; x++)
        {
            AddLine(new Line(new Vector3(x, 0, -GRID_SIZE), new Vector3(x, 0, GRID_SIZE), new Vector4(0f, 0f, 0f, 0.1f)));
        }

        for (int z = -GRID_SIZE; z <= GRID_SIZE; z++)
        {
            AddLine(new Line(new Vector3(-GRID_SIZE, 0, z), new Vector3(GRID_SIZE, 0, z), new Vector4(0f, 0f, 0f, 0.1f)));
        }
    }

    public void Load()
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
    }

    public unsafe int Render(double delta, Matrix4x4 view, Matrix4x4 projection)
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
        return 1;
    }

    public void AddLine(Line line)
    {
        LineVertexOffsets.Add(Lines.Count * 2);
        LineVertexCounts.Add(2);

        LineVertexData.Capacity += 14;

        LineVertexData.Add(0);
        LineVertexData.Add(0);
        LineVertexData.Add(0);

        LineVertexData.Add(0);
        LineVertexData.Add(0);
        LineVertexData.Add(0);
        LineVertexData.Add(0);

        LineVertexData.Add(0);
        LineVertexData.Add(0);
        LineVertexData.Add(0);

        LineVertexData.Add(0);
        LineVertexData.Add(0);
        LineVertexData.Add(0);
        LineVertexData.Add(0);

        Lines.Add(line);
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
}