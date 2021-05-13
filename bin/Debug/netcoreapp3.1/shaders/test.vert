#version 330 core
in vec3 in_position;
in vec3 in_color;
in vec2 in_uv;

out vec3 color;
out vec2 uv;

void main()
{
    gl_Position = vec4(in_position, 1.0);

    color = in_color;
    uv = in_uv;
}