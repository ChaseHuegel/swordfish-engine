#version 330 core
out vec4 FragColor;

in vec4 color;
in vec2 uv;
in vec3 normal;

uniform sampler2D texture0;

void main()
{
    FragColor = texture(texture0, uv);

    if (FragColor.a == 0 || FragColor.rgb == vec3(0, 0, 0))
        discard;
}