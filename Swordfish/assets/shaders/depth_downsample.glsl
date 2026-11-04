#version 430

#ifdef VERTEX
layout (location = 1) in vec2 vUV;
#endif

inout vec2 UV;

uniform sampler2D uDepthTex;
uniform vec2 uOffset;

#ifdef FRAGMENT
vec4 fragment()
{
    //  Average the source block instead of point sampling it. Window depth is
    //  linear across a plane, so the mean of the block equals the depth at the
    //  block centre. A single sample turns an angled plane into a staircase and
    //  the AO reconstructs that staircase as banding.
    float d0 = texture(uDepthTex, UV + vec2( uOffset.x,  uOffset.y)).r;
    float d1 = texture(uDepthTex, UV + vec2(-uOffset.x,  uOffset.y)).r;
    float d2 = texture(uDepthTex, UV + vec2( uOffset.x, -uOffset.y)).r;
    float d3 = texture(uDepthTex, UV + vec2(-uOffset.x, -uOffset.y)).r;

    //  A sky tap is the far plane. Mixing it into the block puts the
    //  reconstructed point on neither surface, so only taps below 1.0 are
    //  averaged. A block with no valid tap stays at the far plane.
    float taps[4] = float[](d0, d1, d2, d3);
    float sum = 0.0;
    float count = 0.0;

    for (int i = 0; i < 4; i++)
    {
        if (taps[i] < 1.0)
        {
            sum += taps[i];
            count += 1.0;
        }
    }

    return vec4(count > 0.0 ? sum / count : 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    UV = vUV;
    return vec4(VertexPosition, 1.0);
}
#endif
