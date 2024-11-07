#version 330 core
out vec4 FragColor;

in vec4 color;
in vec2 uv;
in vec3 normal;

uniform sampler2D texture0;
uniform vec3 Tint = vec3(1);
uniform vec2 Offset = vec2(0);

void main()
{
    FragColor = texture(texture0, uv + Offset) * vec4(Tint, 1);

    if (FragColor.a == 0 || FragColor.rgb == vec3(0, 0, 0))
        discard;
}