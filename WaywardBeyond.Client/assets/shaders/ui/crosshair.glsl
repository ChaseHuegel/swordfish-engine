#version 330 core

uniform sampler2D texture0;

vec4 vertex()
{
    return vec4(VertexPosition, 1.0);
}

vec3 linearToSrgb(vec3 x)
{
    x = clamp(x, 0.0, 1.0);
    vec3 lo = x * 12.92;
    vec3 hi = 1.055 * pow(x, vec3(1.0 / 2.4)) - 0.055;
    return mix(lo, hi, step(vec3(0.0031308), x));
}

vec4 fragment()
{
    vec4 tex = texture(texture0, TextureCoord.xy);
    return VertexColor * vec4(linearToSrgb(tex.rgb), tex.a);
}