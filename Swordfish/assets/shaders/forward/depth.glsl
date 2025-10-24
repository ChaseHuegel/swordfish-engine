#version 430 core

vec4 vertex()
{
    return projection * view * model * vec4(VertexPosition, 1.0);
}

vec4 fragment()
{
    return vec4(0);
}