#version 330 core
out vec4 FragColor;

in vec4 color;
in vec2 uv;

uniform sampler2D texture0;

void main()
{
    FragColor = color * texture(texture0, uv);

    if (FragColor.a == 0)
        discard;
}