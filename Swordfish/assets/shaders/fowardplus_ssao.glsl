#version 430

#ifdef VERTEX
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec2 aTexCoords;
#endif

uniform sampler2D uDepthTex;
uniform ivec2 uScreenSize;
uniform float uRadius = 1.0;
uniform float uBias = 0.01;
uniform mat4 uInvProj;

#ifdef FRAGMENT
layout(location = 0) out float fragAO;

// Pre-baked kernel of 16 samples in view-space
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

vec4 fragment() {
    ivec2 pix = ivec2(gl_FragCoord.xy);
    float depth = texelFetch(uDepthTex, pix, 0).r;
    vec3 posView = UnprojectToView(gl_FragCoord.xy, depth);

    float occlusion = 0.0;

    for(int i = 0; i < KERNEL_SIZE; ++i)
    {
        vec3 samplePos = posView + samples[i] * uRadius;

        // Project back to screen space
        vec4 clip = uInvProj * vec4(samplePos, 1.0);
        clip /= clip.w;
        vec2 sampleUV = clip.xy * 0.5 + 0.5;
        sampleUV = clamp(sampleUV, 0.0, 1.0);

        float sampleDepth = texture(uDepthTex, sampleUV).r;
        vec3 sampleView = UnprojectToView(sampleUV * uScreenSize, sampleDepth);

        float rangeCheck = smoothstep(0.0, 1.0, uRadius / (length(posView - sampleView) + uBias));
        if(sampleView.z >= samplePos.z + uBias)
        occlusion += rangeCheck;
    }

    fragAO = clamp(1.0 - occlusion / float(KERNEL_SIZE), 0.0, 1.0);
    vec3 color = vec3(posView.z * 0.1 + 0.5);
    fragAO = color.r;
    return vec4(color, 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    return vec4(aPos, 1.0);
}
#endif