#version 330 core
out vec4 FragColor;

in vec4 color;
in vec3 uv;

uniform sampler2DArray texture0;

void main()
{
    FragColor = color * texture(texture0, uv);
}