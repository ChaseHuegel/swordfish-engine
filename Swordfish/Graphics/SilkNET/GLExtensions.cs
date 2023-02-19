using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET;

internal static class GLExtensions
{
    public const string FRAGMENT_DIRECTIVE = "FRAGMENT";
    public const string VERTEX_DIRECTIVE = "VERTEX";
    public const string GEOMETRY_DIRECTIVE = "GEOMETRY";
    public const string TESS_EVAL_DIRECTIVE = "TESS_EVALUATION";
    public const string TESS_CTRL_DIRECTIVE = "TESS_CONTROL";
    public const string COMPUTE_DIRECTIVE = "COMPUTE";

    public static string GetDirective(this ShaderType shaderType)
    {
        return shaderType switch
        {
            ShaderType.FragmentShader => FRAGMENT_DIRECTIVE,
            ShaderType.VertexShader => VERTEX_DIRECTIVE,
            ShaderType.GeometryShader => GEOMETRY_DIRECTIVE,
            ShaderType.TessEvaluationShader => TESS_EVAL_DIRECTIVE,
            ShaderType.TessControlShader => TESS_CTRL_DIRECTIVE,
            ShaderType.ComputeShader => COMPUTE_DIRECTIVE,
            _ => throw new NotImplementedException(),
        };
    }
}
