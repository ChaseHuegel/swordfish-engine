#version 330 core

uniform vec4 color = vec4(1, 1, 1, 1);

vec4 vertex()
{
    return projection * view * vec4(VertexPosition, 1.0);
}

vec4 fragment()
{
    return color;
}