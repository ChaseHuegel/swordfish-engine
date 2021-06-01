#version 330 core
in vec3 in_position;
in vec4 in_color;

out vec4 color;

uniform mat4 transform;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    gl_Position = (vec4(in_position, 1.0) * transform * view * projection);

    color = in_color;
}