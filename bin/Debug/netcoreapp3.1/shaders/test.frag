#version 330 core
out vec4 FragColor;

in vec3 color;
in vec2 uv;

uniform sampler2D texture0;

void main()
{
    FragColor = vec4(color, 1.0) * texture(texture0, uv);
}