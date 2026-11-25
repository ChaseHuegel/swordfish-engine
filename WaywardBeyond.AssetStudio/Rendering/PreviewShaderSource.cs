namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Provides shared unlit preview shader sources with desktop GL 330 core and GLES 300 es preambles.</summary>
public static class PreviewShaderSource
{
    private const string DESKTOP_PREAMBLE = "#version 330 core\n";
    private const string GLES_VERTEX_PREAMBLE = "#version 300 es\nprecision highp float;\n";
    private const string GLES_FRAGMENT_PREAMBLE = "#version 300 es\nprecision highp float;\nprecision highp sampler2D;\nprecision highp sampler2DArray;\n";

    private const string VERTEX_BODY = """
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec4 aColor;
        layout(location = 2) in vec3 aUv;
        layout(location = 3) in vec3 aNormal;

        uniform mat4 uMvp;

        out vec4 vColor;
        out vec3 vUv;
        out vec3 vNormal;

        void main()
        {
            vColor = aColor;
            vUv = aUv;
            vNormal = aNormal;
            gl_Position = uMvp * vec4(aPosition, 1.0);
        }
        """;

    private const string FRAGMENT_BODY = """
        uniform sampler2DArray uAlbedo;
        uniform sampler2D uTexture2D;
        uniform sampler2D uReferenceTexture2D;
        uniform int uTextureMode;
        uniform float uAlphaCutoff;

        in vec4 vColor;
        in vec3 vUv;
        in vec3 vNormal;

        out vec4 fragColor;

        void main()
        {
            if (uTextureMode == 2)
            {
                fragColor = vColor;
                return;
            }

            vec4 albedo;
            if (uTextureMode == 1)
            {
                albedo = texture(uTexture2D, vUv.xy);
            }
            else if (uTextureMode == 3)
            {
                albedo = texture(uReferenceTexture2D, vUv.xy);
            }
            else
            {
                albedo = texture(uAlbedo, vUv);
            }

            if (uAlphaCutoff > 0.0 && albedo.a < uAlphaCutoff)
            {
                discard;
            }

            fragColor = vec4(albedo.rgb * vColor.rgb * vColor.a, albedo.a);
        }
        """;

    /// <summary>Returns the unlit vertex shader source for desktop OpenGL or OpenGL ES.</summary>
    public static string GetVertexShader(bool isOpenGlEs)
    {
        return (isOpenGlEs ? GLES_VERTEX_PREAMBLE : DESKTOP_PREAMBLE) + VERTEX_BODY;
    }

    /// <summary>Returns the unlit fragment shader source for desktop OpenGL or OpenGL ES.</summary>
    public static string GetFragmentShader(bool isOpenGlEs)
    {
        return (isOpenGlEs ? GLES_FRAGMENT_PREAMBLE : DESKTOP_PREAMBLE) + FRAGMENT_BODY;
    }
}
