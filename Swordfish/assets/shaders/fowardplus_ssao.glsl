#version 430

#ifdef VERTEX
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec2 aTexCoords;
#endif

inout vec2 TexCoords;

uniform sampler2D uDepthTex;
uniform ivec2 uScreenSize;
uniform float uRadius = 10.0;
uniform float uBias = 0.01;
uniform mat4 uInvProj;
uniform float near;
uniform float far;

#ifdef FRAGMENT

const int KERNEL_SIZE = 16;
const vec3 samples[KERNEL_SIZE] = vec3[](
    vec3(0.5381, 0.1856, 0.4319),
    vec3(0.1379, 0.2486, 0.4430),
    vec3(0.3371, 0.5679, 0.0057),
    vec3(-0.6999, -0.0451, -0.0019),
    vec3(0.0689, -0.1598, -0.8547),
    vec3(0.0560, 0.0069, -0.1843),
    vec3(-0.0146, 0.1402, 0.0762),
    vec3(0.0100, -0.1924, -0.0344),
    vec3(-0.3577, -0.5301, -0.4358),
    vec3(-0.3169, 0.1063, 0.0158),
    vec3(0.0103, -0.5869, 0.0046),
    vec3(-0.0897, -0.4940, 0.3287),
    vec3(0.7119, -0.0154, -0.0918),
    vec3(-0.0533, 0.0596, -0.5411),
    vec3(0.0352, -0.0631, 0.5460),
    vec3(-0.4776, 0.2847, -0.0271)
);

// Reconstruct view-space position
vec3 UnprojectToView(vec2 pixel, float depth)
{
    vec2 ndc = (pixel / uScreenSize) * 2.0 - 1.0;
    vec4 clip = vec4(ndc, depth * 2.0 - 1.0, 1.0);
    vec4 view = uInvProj * clip;
    return view.xyz / view.w;
}

float LinearDepth(float depth)
{
    float z = depth * 2.0 - 1.0;
    return (near * far) / (far + near - z * (far - near));
}

vec4 fragment() {
    float depth = texture(uDepthTex, TexCoords).r;
    if (depth >= 1.0) {
        return vec4(1.0);
    }

    vec3 posView = UnprojectToView(TexCoords, depth);

    vec2 texel = 1.0 / textureSize(uDepthTex, 0);
    float depthR = texture(uDepthTex, TexCoords + vec2(texel.x, 0)).r;
    float depthU = texture(uDepthTex, TexCoords + vec2(0, texel.y)).r;
    vec3 pR = UnprojectToView(TexCoords + vec2(texel.x, 0), depthR);
    vec3 pU = UnprojectToView(TexCoords + vec2(0, texel.y), depthU);
    vec3 N = normalize(cross(pR - posView, pU - posView));

    vec3 T = normalize(pR - posView);
    vec3 B = cross(N, T);
    mat3 TBN = mat3(T, B, N);

    float occlusion = 0.0;
    for (int i = 0; i < KERNEL_SIZE; i++) {
        vec3 samplePos = posView + TBN * (samples[i] * uRadius);

        vec4 offset = inverse(uInvProj) * vec4(samplePos, 1.0);
        offset.xyz /= offset.w;
        vec2 uv = offset.xy * 0.5 + 0.5;

        float sampleDepth = texture(uDepthTex, uv).r;
        if (sampleDepth >= 1.0)
            continue;

        vec3 neighborPos = UnprojectToView(uv, sampleDepth);

        float dz = samplePos.z - neighborPos.z;
        float occ = smoothstep(0.0, uRadius, dz);

        if (dz > uBias)
            occlusion += occ;
    }

    occlusion /= float(KERNEL_SIZE);

    float ao = 1.0 - occlusion;
    return vec4(ao, ao, ao, 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    TexCoords = aTexCoords;
    return vec4(aPos, 1.0);
}
#endif