#version 330 core
out vec4 FragColor;

in vec2 uv;

uniform sampler2D texture0;

const float offset = 1.0 / 300.0;

void main()
{
    vec3 hdr = texture(texture0, uv).rgb;

    vec2 offsets[9] = vec2[]
    (
        vec2(-offset,  offset),
        vec2( 0,    offset),
        vec2( offset,  offset),
        vec2(-offset,  0),
        vec2( 0,    0),
        vec2( offset,  0),
        vec2(-offset, -offset),
        vec2( 0,   -offset),
        vec2( offset, -offset)
    );

    float kernel[9] = float[]
    (
        1.0 / 16, 2.0 / 16, 1.0 / 16,
        2.0 / 16, 4.0 / 16, 2.0 / 16,
        1.0 / 16, 2.0 / 16, 1.0 / 16
    );

    vec3 sample[9];
    for(int i = 0; i < 9; i++)
        sample[i] = vec3(texture(texture0, uv.st + offsets[i]));

    for(int i = 0; i < 9; i++)
        hdr += sample[i] * kernel[i];

    FragColor = vec4(hdr, 1);
}