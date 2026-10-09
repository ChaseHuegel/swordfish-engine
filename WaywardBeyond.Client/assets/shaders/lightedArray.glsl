#version 430 core
#include "pbr_plus.glsl"

#ifdef FRAGMENT
uniform sampler2DArray texture0;
uniform sampler2DArray texture1;
uniform sampler2DArray texture2;
uniform sampler2DArray texture3;
uniform sampler2DArray texture4;

uniform float uBloomThreshold = 1.5;
uniform float uBloomSoftness = 0.5;

vec3 GetNormal()
{
    vec3 tangentNormal = texture(texture3, TextureCoord).xyz * 2.0 - 1.0;

    vec3 Q1 = dFdx(vWorldPos);
    vec3 Q2 = dFdy(vWorldPos);
    vec2 st1 = dFdx(TextureCoord.xy);
    vec2 st2 = dFdy(TextureCoord.xy);

    vec3 N = vNormal;
    vec3 T = normalize(Q1 * st2.t - Q2 * st1.t);
    vec3 B = -normalize(cross(N, T));
    mat3 TBN = mat3(T, B, N);

    return normalize(TBN * tangentNormal);
}

vec4 fragment()
{
    vec4 albedo = texture(texture0, TextureCoord);
    Metallic = texture(texture1, TextureCoord).r;
    Roughness = 1.0 - texture(texture2, TextureCoord).r;
    Normal = GetNormal();
    vec4 emissive = texture(texture4, TextureCoord);
    vec4 color = max(albedo * VertexColor * shade(albedo.rgb) * ao(albedo), emissive);
    
    if (length(emissive) > 0.0)
    {
        BrightColor = emissive;
    }
    else
    {
        BrightColor = vec4(0.0, 0.0, 0.0, 1.0);
    }

    return color;
}
#endif