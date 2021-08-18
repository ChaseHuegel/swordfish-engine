#version 330 core
out vec4 FragColor;

in vec4 color;
in vec2 uv;
in vec3 normal;

uniform sampler2D texture0;

void main()
{
    vec3 result = pow(color.rgb * texture(texture0, uv).rgb, vec3(1.0/2.2));
    FragColor = vec4(result, texture(texture0, uv).a);

    if (FragColor.a == 0 || FragColor.rgb == vec3(0, 0, 0))
        discard;
}