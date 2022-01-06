#version 330 core
in vec3 in_position;

uniform mat4 transform;
uniform mat4 inversedTransform;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    gl_Position = vec4(in_position, 1.0) * transform * view * projection;
}