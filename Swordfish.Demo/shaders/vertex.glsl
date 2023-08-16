#version 330 core

vec4 vertex()
{
    return projection * view * model * vec4(VertexPosition, 1.0);
}