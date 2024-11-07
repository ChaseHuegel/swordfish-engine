#version 330 core
layout (location = 0) in vec3 in_position;
layout (location = 1) in vec4 in_color;
layout (location = 2) in vec3 in_uv;

out vec4 out_color;
out vec3 out_uv;

void main()
{
    gl_Position = vec4(in_position, 1.0);
    
    out_color = in_color;
    out_uv = in_uv;
}