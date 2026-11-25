#version 430 core
#include "pbr_plus.glsl"

#ifdef FRAGMENT
uniform sampler2DArray texture0;

vec4 fragment()
{
    vec4 albedo = texture(texture0, TextureCoord);
    if (albedo.a < 0.05)
    {
        discard;
    }

    vec3 n = normalize(gl_FrontFacing ? vNormal : -vNormal);
    float faceLight = 0.58 + 0.42 * abs(n.y) + 0.14 * max(n.x, -n.z);
    vec3 rgb = albedo.rgb * VertexColor.rgb * clamp(faceLight, 0.55, 1.05);
    float alpha = clamp(albedo.a * VertexColor.a, 0.0, 1.0);
    BrightColor = vec4(rgb * 0.18, alpha);
    return vec4(rgb, alpha);
}
#endif
