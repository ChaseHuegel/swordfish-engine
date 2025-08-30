#version 330 core

vec4 vertex()
{
    return vec4(VertexPosition, 1.0);
}

vec4 fragment()
{
    return VertexColor;
}