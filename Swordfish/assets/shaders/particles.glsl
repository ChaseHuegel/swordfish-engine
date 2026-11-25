#version 330 core

inout vec4 vParticleColor;
inout vec2 vParticleUV;
inout float vParticleEmissive;

#ifdef VERTEX
layout (location = 8) in vec4 in_particlePosSize;
layout (location = 9) in vec4 in_particleColor;
layout (location = 10) in vec2 in_particleRotEmissive;
#endif

#ifdef FRAGMENT
uniform sampler2D texture0;
uniform int uHasTexture = 0;
#endif

vec4 vertex()
{
    vec4 viewCenter = view * vec4(in_particlePosSize.xyz, 1.0);
    float angle = in_particleRotEmissive.x;
    float c = cos(angle);
    float s = sin(angle);
    vec2 scaled = in_position.xy * in_particlePosSize.w;
    vec2 rotated = vec2(
        scaled.x * c - scaled.y * s,
        scaled.x * s + scaled.y * c
    );
    viewCenter.xy += rotated;

    vParticleColor = in_particleColor;
    vParticleUV = in_uv.xy;
    vParticleEmissive = in_particleRotEmissive.y;
    return projection * viewCenter;
}

vec4 fragment()
{
    vec4 color = vParticleColor;
    if (uHasTexture != 0)
    {
        color *= texture(texture0, vParticleUV);
    }

    if (color.a <= 0.001)
    {
        discard;
    }

    vec3 hdrRgb = color.rgb * max(1.0, vParticleEmissive);
    vec3 brightRgb = color.rgb * max(0.0, vParticleEmissive);
    BrightColor = vec4(brightRgb, color.a);
    return vec4(hdrRgb, color.a);
}
