#version 330 core
out vec4 FragColor;

in vec2 uv;

uniform sampler2D texture0;

void main()
{
    vec3 color = texture(texture0, uv).rgb;

    //  HDR
    // color = color / (color + vec3(1.0));

    //  Gamma correction
    color = pow(color, vec3(1.0/2.2));

    FragColor = vec4(color, 1);
}