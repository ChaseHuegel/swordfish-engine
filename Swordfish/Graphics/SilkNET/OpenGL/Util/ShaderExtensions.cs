namespace Swordfish.Graphics.SilkNET.OpenGL.Util;

internal static class ShaderExtensions
{
    public static ShaderProgram CreateProgram(this Shader shader, GLContext glContext)
    {
        ShaderComponent[] shaderComponents = shader.Sources.Select(shaderSource => shaderSource.CreateComponent(glContext)).ToArray();
        return glContext.CreateShaderProgram(shader.Name, shaderComponents);
    }
    
    public static ShaderComponent CreateComponent(this ShaderSource shaderSource, GLContext glContext)
    {
        return glContext.CreateShaderComponent(shaderSource.Name, shaderSource.Type.ToSilkShaderType(), shaderSource.Source);
    }
}