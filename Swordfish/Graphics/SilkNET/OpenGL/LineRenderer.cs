using System.Collections.Concurrent;
using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.IO;
using Shader = Swordfish.Graphics.Shader;

internal class LineRenderer : IRenderStage
{
    private ShaderProgram? ShaderProgram;
    private VertexArrayObject<float>? VAO;
    private readonly float[] Vertices = new float[6];
    private readonly List<Line> Lines = new();

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

        Lines.Add(new Line(Vector3.Zero, new Vector3(10000, 0, 0), new Vector4(1f, 0f, 0f, 1f)));
        Lines.Add(new Line(Vector3.Zero, new Vector3(-10000, 0, 0), new Vector4(1f, 0f, 0f, 0.25f)));

        Lines.Add(new Line(Vector3.Zero, new Vector3(0, 10000, 0), new Vector4(0f, 1f, 0f, 1f)));
        Lines.Add(new Line(Vector3.Zero, new Vector3(0, -10000, 0), new Vector4(0f, 1f, 0f, 0.25f)));

        Lines.Add(new Line(Vector3.Zero, new Vector3(0, 0, 10000), new Vector4(0f, 0f, 1f, 1f)));
        Lines.Add(new Line(Vector3.Zero, new Vector3(0, 0, -10000), new Vector4(0f, 0f, 1f, 0.25f)));

        for (int x = -100; x <= 100; x++)
        {
            Lines.Add(new Line(new Vector3(x, 0, -100), new Vector3(x, 0, 100), new Vector4(0f, 0f, 0f, 0.1f)));
        }

        for (int z = -100; z <= 100; z++)
        {
            Lines.Add(new Line(new Vector3(-100, 0, z), new Vector3(100, 0, z), new Vector4(0f, 0f, 0f, 0.1f)));
        }
    }

    public void Load()
    {
        Shader shader = FileService.Parse<Shader>(PathService.Shaders.At("lines.glsl"));
        VAO = GLContext.CreateVertexArrayObject(Vertices);

        VAO.Bind();
        VAO.VertexBufferObject.Bind();
        VAO.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, 3, 0);

        ShaderProgram = ShaderToShaderProgram(shader);
        ShaderProgram.BindAttributeLocation("in_position", 0);
    }

    public int Render(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        int drawCalls = 0;
        for (int i = 0; i < Lines.Count; i++)
        {
            Line line = Lines[i];

            ShaderProgram!.Activate();
            ShaderProgram.SetUniform("view", view);
            ShaderProgram.SetUniform("projection", projection);
            ShaderProgram.SetUniform("color", line.Color);

            VAO!.Bind();

            Vertices[0] = line.Start.X;
            Vertices[1] = line.Start.Y;
            Vertices[2] = line.Start.Z;
            Vertices[3] = line.End.X;
            Vertices[4] = line.End.Y;
            Vertices[5] = line.End.Z;
            VAO.VertexBufferObject.UpdateData(Vertices);

            GL.DrawArrays(PrimitiveType.Lines, 0, 2);
            drawCalls++;
        }

        return drawCalls;
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