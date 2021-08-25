#version 330 core
out vec4 FragColor;

in vec4 color;
in vec2 uv;
in vec3 normal;

uniform sampler2D texture0;
uniform vec3 Tint = vec3(1);

void main()
{
    FragColor = texture(texture0, uv) * vec4(Tint, 1);

    if (FragColor.a == 0 || FragColor.rgb == vec3(0, 0, 0))
        discard;
}