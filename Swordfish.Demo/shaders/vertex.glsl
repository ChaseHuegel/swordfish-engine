#version 330 core

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

vec4 vertex()
{
    return projection * view * model * vec4(VertexPosition, 1.0);
}