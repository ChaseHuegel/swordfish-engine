#version 330 core

uniform sampler2D texture0;

vec4 vertex()
{
    return vec4(VertexPosition, 1.0);
}

vec4 fragment()
{
    return VertexColor * texture(texture0, TextureCoord.xy);
}