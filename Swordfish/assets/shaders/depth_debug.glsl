#version 430

#ifdef VERTEX
layout (location = 1) in vec2 vUV;
#endif

inout vec2 UV;

uniform sampler2D uDepthTex;
uniform vec2 uInvSize;
uniform float uDepthA;
uniform float uDepthB;

#ifdef FRAGMENT
float ViewDistance(vec2 uv)
{
    float depth = texture(uDepthTex, uv).r;
    if (depth >= 1.0)
    {
        return -1.0;
    }

    float ndcZ = depth * 2.0 - 1.0;
    return uDepthA / (uDepthB + ndcZ);
}

vec4 fragment()
{
    float center = ViewDistance(UV);
    if (center <= 0.0)
    {
        return vec4(0.0, 0.0, 0.0, 1.0);
    }

    float right = ViewDistance(UV + vec2(uInvSize.x, 0.0));
    float down = ViewDistance(UV + vec2(0.0, uInvSize.y));

    float dx = right > 0.0 ? abs(right - center) : 0.0;
    float dy = down > 0.0 ? abs(down - center) : 0.0;

    //  Edge magnitude in metres, scaled so a 1 cm step is white.
    float edge = clamp((dx + dy) * 100.0, 0.0, 1.0);
    return vec4(edge, edge, edge, 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    UV = vUV;
    return vec4(VertexPosition, 1.0);
}
#endif
