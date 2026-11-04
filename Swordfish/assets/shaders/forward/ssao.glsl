#version 430

#ifdef VERTEX
layout (location = 1) in vec2 vUV;
#endif

inout vec2 UV;

uniform sampler2D uDepthTex;
uniform vec2 uViewScale;
uniform float uDepthA;
uniform float uDepthB;
uniform vec2 uInvSize;
uniform float uAspect;

uniform float uRadius = 0.1;
uniform float SIGMA = 0.5;
uniform int uSampleCount = 8;
uniform float uMaxDistance = 48.0;
uniform float uMaxDistanceFade = 8.0;

#ifdef FRAGMENT

const vec2 NOISE[16] = vec2[16] (
    vec2(-0.6461,  0.7633), vec2( 0.1163, -0.9932), vec2( 0.3344,  0.9424), vec2( 0.7127, -0.7014),
    vec2(-0.8596, -0.5109), vec2(-0.9344, -0.3563), vec2( 0.7430,  0.6693), vec2( 0.2133,  0.9770),
    vec2( 0.5804,  0.8143), vec2( 0.5128, -0.8585), vec2( 0.8621, -0.5068), vec2(-0.8259, -0.5638),
    vec2( 0.6577, -0.7533), vec2(-0.5417, -0.8406), vec2(-0.9951, -0.0987), vec2(-0.8237,  0.5671)
);

vec3 ViewPosition(vec2 uv, float depth)
{
    float ndcZ = depth * 2.0 - 1.0;
    float viewDistance = uDepthA / (uDepthB + ndcZ);
    vec2 ndc = uv * 2.0 - 1.0;
    return vec3(ndc * uViewScale * viewDistance, -viewDistance);
}

vec2 GetNoise()
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    int idx = (pixel.x & 3) + ((pixel.y & 3) << 2);
    return NOISE[idx];
}

float SAO(vec2 xy, vec3 verPos, vec3 n, vec2 noise, float radius)
{
    float g = 1.32471795724474602596; // plastic constant
    float vl = length(verPos);

    vec2 aspectCorrection = vec2(1.0, uAspect);

    vec2 acc = vec2(0.0);
    for (int i = 0; i < uSampleCount; i++)
    {
        vec2 ns = vec2(
            6.2831853 * ((noise.x + float(i)) / float(uSampleCount)),
            fract(noise.y + float(i) / g) * radius / vl
        );

        vec2 nxy = xy + ns.y * vec2(cos(ns.x), sin(ns.x)) * aspectCorrection;

        float sampleDepth = texture(uDepthTex, nxy).r;
        if (sampleDepth >= 1.0)
        {
            continue;
        }

        vec3 samPos = ViewPosition(nxy, sampleDepth);
        vec3 tv = samPos - verPos;

        acc += vec2(max(0.0, dot(tv, n)) / (dot(tv, tv) + 0.1), 1.0);
    }

    if (acc.y < 1.0)
    {
        return 1.0;
    }

    // pow(visibility, 1.5) without the transcendental.
    float visibility = clamp(1.0 - SIGMA * 2.0 * acc.x / acc.y, 0.0, 1.0);
    return visibility * sqrt(visibility);
}

vec4 fragment()
{
    float depth = texture(uDepthTex, UV).r;
    if (depth >= 1.0)
    {
        return vec4(1.0);
    }

    vec3 posVS = ViewPosition(UV, depth);
    float viewDistance = length(posVS);
    if (viewDistance > uMaxDistance)
    {
        return vec4(1.0);
    }

    float depthRight = texture(uDepthTex, UV + vec2(uInvSize.x, 0.0)).r;
    float depthDown = texture(uDepthTex, UV + vec2(0.0, uInvSize.y)).r;
    vec3 posX = ViewPosition(UV + vec2(uInvSize.x, 0.0), depthRight);
    vec3 posY = ViewPosition(UV + vec2(0.0, uInvSize.y), depthDown);
    vec3 n = normalize(cross(posX - posVS, posY - posVS));

    vec2 noise = GetNoise();
    float ao = SAO(UV, posVS, n, noise, uRadius);

    float fade = 1.0 - smoothstep(uMaxDistance - uMaxDistanceFade, uMaxDistance, viewDistance);
    ao = mix(1.0, ao, fade);
    return vec4(ao, ao, ao, 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    UV = vUV;
    return vec4(VertexPosition, 1.0);
}
#endif
