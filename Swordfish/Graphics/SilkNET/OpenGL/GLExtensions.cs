namespace Swordfish.Graphics.SilkNET.OpenGL;

internal static class GLExtensions
{
    public const string FRAGMENT_DIRECTIVE = "FRAGMENT";
    public const string VERTEX_DIRECTIVE = "VERTEX";
    public const string GEOMETRY_DIRECTIVE = "GEOMETRY";
    public const string TESS_EVAL_DIRECTIVE = "TESS_EVALUATION";
    public const string TESS_CTRL_DIRECTIVE = "TESS_CONTROL";
    public const string COMPUTE_DIRECTIVE = "COMPUTE";

    public static string GetDirective(this Silk.NET.OpenGL.ShaderType shaderType)
    {
        return shaderType switch
        {
            Silk.NET.OpenGL.ShaderType.FragmentShader => FRAGMENT_DIRECTIVE,
            Silk.NET.OpenGL.ShaderType.VertexShader => VERTEX_DIRECTIVE,
            Silk.NET.OpenGL.ShaderType.GeometryShader => GEOMETRY_DIRECTIVE,
            Silk.NET.OpenGL.ShaderType.TessEvaluationShader => TESS_EVAL_DIRECTIVE,
            Silk.NET.OpenGL.ShaderType.TessControlShader => TESS_CTRL_DIRECTIVE,
            Silk.NET.OpenGL.ShaderType.ComputeShader => COMPUTE_DIRECTIVE,
            _ => throw new NotImplementedException(),
        };
    }

    public static Silk.NET.OpenGL.ShaderType ToSilkShaderType(this ShaderType shaderType)
    {
        return shaderType switch
        {
            ShaderType.Vertex => Silk.NET.OpenGL.ShaderType.VertexShader,
            ShaderType.Fragment => Silk.NET.OpenGL.ShaderType.FragmentShader,
            ShaderType.Compute => Silk.NET.OpenGL.ShaderType.ComputeShader,
            ShaderType.Geometry => Silk.NET.OpenGL.ShaderType.GeometryShader,
            _ => throw new NotImplementedException(),
        };
    }
}
