#version 430 core
#include "pbr_plus.glsl"

#ifdef FRAGMENT
uniform sampler2D texture0;
uniform float uAlphaCutoff = 0.0;

vec4 fragment()
{
    vec4 albedo = texture(texture0, TextureCoord.xy);
    if (albedo.a < uAlphaCutoff)
    {
        discard;
    }

    Metallic = uMetallic;
    Roughness = uRoughness;
    Normal = normalize(gl_FrontFacing ? vNormal : -vNormal);

    vec4 shaded = vec4(shade(albedo.rgb).rgb, albedo.a);
    vec4 color = shaded * VertexColor * ao(albedo);
    BrightColor = vec4(0.0, 0.0, 0.0, color.a);
    return color;
}
#endif
