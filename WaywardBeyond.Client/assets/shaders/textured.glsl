#version 330 core
#include "vertex.glsl"

uniform sampler2D texture0;

vec4 fragment()
{
    return texture(texture0, TextureCoord.xy) * VertexColor;
}