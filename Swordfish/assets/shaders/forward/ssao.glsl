#version 430

#ifdef VERTEX
layout (location = 1) in vec2 vUV;
#endif

inout vec2 UV;

uniform sampler2D uDepthTex;
uniform mat4 uInvProj;

uniform float uRadius = 0.1;
uniform float SIGMA = 0.5;
uniform float SAO_K = 1.0;

#ifdef FRAGMENT

const vec2 NOISE[16] = vec2[16](
    vec2(-0.6461,  0.7633), vec2( 0.1163, -0.9932), vec2( 0.3344,  0.9424), vec2( 0.7127, -0.7014),
    vec2(-0.8596, -0.5109), vec2(-0.9344, -0.3563), vec2( 0.7430,  0.6693), vec2( 0.2133,  0.9770),
    vec2( 0.5804,  0.8143), vec2( 0.5128, -0.8585), vec2( 0.8621, -0.5068), vec2(-0.8259, -0.5638),
    vec2( 0.6577, -0.7533), vec2(-0.5417, -0.8406), vec2(-0.9951, -0.0987), vec2(-0.8237,  0.5671)
);

vec3 ReconstructVSPos(vec2 uv, float depth)
{
    vec4 ndc = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 view = uInvProj * ndc;
    return view.xyz / view.w;
}

vec2 GetNoise()
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    ivec2 p = pixel % 4;
    int idx = p.x + p.y * 4;
    return NOISE[idx];
}

float SAO(vec2 xy, vec3 verPos, vec3 n, vec2 noise, float radius)
{
    float g = 1.32471795724474602596; // plastic constant
    vec2 ng = 1.0 / vec2(g, g * g);
    float vl = length(verPos);

    vec2 textureDimensions = textureSize(uDepthTex, 0);
    vec2 aspectCorrection = vec2(1.0, textureDimensions.x / textureDimensions.y);
    
    vec2 acc = vec2(0.0);
    for (int i = 0; i < 16; i++)
    {
        vec2 ns = vec2(
            6.2831853 * ((noise.x + float(i)) / 16.0),
            fract(noise.y + float(i) / g) * radius / vl
        );

        vec2 nxy = xy + ns.y * vec2(sin(ns.x), cos(ns.x)) * aspectCorrection;
        
        float sampleDepth = texture(uDepthTex, nxy).r;
        if (sampleDepth >= 1.0)
        {
            continue;
        }
       
        vec3 samPos = ReconstructVSPos(nxy, sampleDepth);
        vec3 tv = samPos - verPos;

        acc += vec2(max(0.0, dot(tv, n)) / (dot(tv, tv) + 0.1), 1.0);
    }

    return pow(clamp(1.0 - SIGMA * 2.0 * acc.x / acc.y, 0.0, 1.0), SAO_K);
}

vec4 fragment()
{
    float depth = texture(uDepthTex, UV).r;
    if (depth >= 1.0) {
        return vec4(1);
    }

    vec3 posVS = ReconstructVSPos(UV, depth);

    vec2 texelSize = 1.0 / textureSize(uDepthTex, 0);
    float depthRight = texture(uDepthTex, UV + vec2(texelSize.x, 0)).r;
    float depthDown  = texture(uDepthTex, UV + vec2(0, texelSize.y)).r;
    vec3 posX = ReconstructVSPos(UV + vec2(texelSize.x, 0), depthRight);
    vec3 posY = ReconstructVSPos(UV + vec2(0, texelSize.y), depthDown);
    vec3 n = normalize(cross(posX - posVS, posY - posVS));

    vec2 noise = GetNoise();
    float ao = SAO(UV, posVS, n, noise, uRadius);
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