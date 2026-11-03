#version 430

#ifdef VERTEX
layout (location = 1) in vec2 vUV;
#endif

inout vec2 UV;

#ifdef FRAGMENT
uniform sampler2D texture0;
uniform float uIntensity;

vec4 fragment()
{
    //  Alpha carries the emissive mask from the extract pass and softens with
    //  the blur. It keeps bloom off the surface that produced it.
    vec4 bloom = texture(texture0, UV);
    return vec4(bloom.rgb * bloom.a * uIntensity, 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    UV = vUV;
    return vec4(VertexPosition, 1.0);
}
#endif