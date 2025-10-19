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

vec4 fragment()
{
    vec4 diffuse = texture(texture0, TextureCoord);
    Metallic = texture(texture1, TextureCoord).r;
    Roughness = 1.0 - texture(texture2, TextureCoord).r;
    Normal = vNormal * (texture(texture3, TextureCoord).rgb * 2.0 - 1.0);
    vec4 emissive = texture(texture4, TextureCoord);
    vec4 color = max(diffuse * VertexColor * shade(), emissive);
    
    if (length(emissive.xyz) > 0.0)
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