#version 430

#ifdef VERTEX
layout (location = 1) in vec2 vUV;
#endif

inout vec2 UV;

#ifdef FRAGMENT
uniform sampler2D texture0;
uniform float uThreshold;
uniform float uSoftness;

vec4 fragment()
{
    vec4 emissive = texture(texture0, UV);
    float brightness = max(emissive.r, max(emissive.g, emissive.b));

    //  A soft knee keeps dim emissive out of the blur without a hard edge.
    const float MIN_EMISSIVE = 1.0 / (255.0 * 12.92);
    float knee = max(uSoftness, MIN_EMISSIVE);
    float contribution = smoothstep(uThreshold, uThreshold + knee, brightness);

    //  Bloom must not cover the emissive surface that produced it. The mask
    //  rides in alpha and widens with the blur.
    float mask = 1.0 - step(MIN_EMISSIVE, brightness);

    return vec4(emissive.rgb * contribution, mask);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    UV = vUV;
    return vec4(VertexPosition, 1.0);
}
#endif
