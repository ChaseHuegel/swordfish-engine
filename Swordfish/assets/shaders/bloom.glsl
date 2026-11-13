#version 430

#ifdef VERTEX
layout (location = 1) in vec2 vUV;
#endif

inout vec2 UV;

#ifdef FRAGMENT
uniform sampler2D uSceneTex;
uniform sampler2D uBloomTex;
uniform float uIntensity;

vec3 tonemapPbrNeutral(vec3 color)
{
    const float START_COMPRESSION = 0.8 - 0.04;
    const float DESATURATION = 0.15;

    float x = min(color.r, min(color.g, color.b));
    float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
    color -= vec3(offset);

    float peak = max(color.r, max(color.g, color.b));
    if (peak < START_COMPRESSION)
    {
        return color;
    }

    const float D = 1.0 - START_COMPRESSION;
    float newPeak = 1.0 - (D * D) / (peak + D - START_COMPRESSION);
    color *= newPeak / peak;

    float g = 1.0 - 1.0 / (DESATURATION * (peak - newPeak) + 1.0);
    return mix(color, vec3(newPeak), g);
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
    vec3 hdr = texture(uSceneTex, UV).rgb;
    if (uIntensity > 0.0)
    {
        vec4 bloom = texture(uBloomTex, UV);
        hdr += bloom.rgb * bloom.a * uIntensity;
    }

    vec3 mapped = tonemapPbrNeutral(max(hdr, vec3(0.0)));
    return vec4(linearToSrgb(mapped), 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    UV = vUV;
    return vec4(VertexPosition, 1.0);
}
#endif
