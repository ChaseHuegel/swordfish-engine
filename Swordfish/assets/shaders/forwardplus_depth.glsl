#version 430 core

#ifdef VERTEX
layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inNormal;
#endif

vec4 vertex()
{
    return projection * view * model * vec4(VertexPosition, 1.0);
}

vec4 fragment()
{
    return vec4(0);
}