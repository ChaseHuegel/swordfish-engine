#version 330 core
out vec4 FragColor;

uniform vec3 Tint = vec3(1);

void main()
{
    FragColor = vec4(Tint, 1);
}