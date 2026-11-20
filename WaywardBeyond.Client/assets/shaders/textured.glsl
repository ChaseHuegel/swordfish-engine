#version 330 core
#include "vertex.glsl"

uniform sampler2D texture0;
uniform float uAlphaCutoff = 0.0;

vec4 fragment()
{
    vec4 color = texture(texture0, TextureCoord.xy) * VertexColor;
    if (color.a < uAlphaCutoff)
    {
        discard;
    }

    return color;
}