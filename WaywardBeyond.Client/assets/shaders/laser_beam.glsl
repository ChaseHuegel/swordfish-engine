#version 430 core

inout vec3 vWorldPos;
inout vec3 vWorldNormal;
inout vec2 vBeamUV;
inout float vLayer;

#ifdef FRAGMENT
uniform vec3 uCameraPos;
uniform float uTime = 0.0;
#endif

vec4 vertex()
{
    vec4 world = model * vec4(VertexPosition, 1.0);
    vWorldPos = world.xyz;
    vWorldNormal = normalize(mat3(model) * VertexNormal);
    vBeamUV = TextureCoord.xy;
    vLayer = TextureCoord.z;
    return projection * view * world;
}

vec4 fragment()
{
    const vec3 WHITE = vec3(1.0);
    const vec3 YELLOW = vec3(0.991102, 0.799103, 0.119538);
    const vec3 ORANGE = vec3(0.991102, 0.423268, 0.034340);
    const vec3 FLAME = vec3(0.930111, 0.181164, 0.015996);
    const vec3 CRIMSON = vec3(0.775822, 0.043735, 0.057805);

    vec3 viewDir = normalize(uCameraPos - vWorldPos);
    float facing = abs(dot(normalize(vWorldNormal), viewDir));

    float flowA = sin(vBeamUV.y * 28.0 - uTime * 36.0);
    float flowB = sin(vBeamUV.y * 54.0 - uTime * 62.0);
    float surge = 0.85 + 0.15 * (0.65 * flowA + 0.35 * flowB);
    float endFade = smoothstep(0.0, 0.03, vBeamUV.y) * smoothstep(1.0, 0.96, vBeamUV.y);

    if (vLayer < 0.5)
    {
        float coreMask = smoothstep(0.15, 0.65, facing);
        vec3 coreColor = mix(YELLOW, WHITE, smoothstep(0.45, 0.95, facing)) * (3.2 * surge);
        float alpha = coreMask * endFade;
        BrightColor = vec4(coreColor * 0.85, alpha);
        return vec4(coreColor, alpha);
    }

    float sheathMask = pow(facing, 1.6);
    float rimBlend = clamp(0.5 + 0.5 * flowA, 0.0, 1.0);
    vec3 edgeColor = mix(FLAME, CRIMSON, 1.0 - facing);
    vec3 sheathColor = mix(edgeColor, ORANGE, sheathMask * (0.65 + 0.35 * rimBlend)) * (2.2 * surge);
    float sheathAlpha = sheathMask * 0.82 * endFade;
    BrightColor = vec4(sheathColor * 0.95, sheathAlpha);
    return vec4(sheathColor, sheathAlpha);
}
