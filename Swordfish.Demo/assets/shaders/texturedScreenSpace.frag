#version 330 core
in vec4 out_color;
in vec3 out_uv;

uniform sampler2D texture0;

out vec4 FragColor;

void main()
{
    FragColor = texture(texture0, out_uv.xy) * out_color;

    if (FragColor.a == 0 || FragColor.rgb == vec3(0, 0, 0))
        discard;
}