#version 430

#ifdef VERTEX
layout (location = 1) in vec2 vUV;
#endif

inout vec2 UV;

#ifdef FRAGMENT
uniform sampler2D texture0;
uniform float uWeight;

vec4 fragment()
{
    //  Bilinear filtering downsamples and upsamples. The weight balances the
    //  levels that accumulate into the finest one.
    return texture(texture0, UV) * uWeight;
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    UV = vUV;
    return vec4(VertexPosition, 1.0);
}
#endif
