#version 330 core
layout (location = 0) in vec3 in_position;
layout (location = 1) in vec4 in_color;
layout (location = 2) in vec3 in_uv;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec4 out_color;
out vec3 out_uv;

void main()
{
    gl_Position = projection * view * model * vec4(in_position, 1.0);
    
    out_color = in_color;
    out_uv = in_uv;
}