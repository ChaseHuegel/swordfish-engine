#version 430 core

#ifdef FRAGMENT
uniform sampler2D texture0;
uniform float uAlphaCutoff = 0.5;
#endif

vec4 vertex()
{
    return projection * view * model * vec4(VertexPosition, 1.0);
}

vec4 fragment()
{
    vec4 texSample = texture(texture0, TextureCoord.xy);
    if (texSample.a < uAlphaCutoff)
    {
        discard;
    }

    return vec4(0);
}
