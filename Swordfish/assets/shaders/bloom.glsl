#version 430

#ifdef VERTEX
layout (location = 1) in vec2 vUV;
#endif

inout vec2 UV;

#ifdef FRAGMENT
uniform sampler2D texture0;

vec4 fragment()
{
    gl_FragDepth = 0.0;
    return texture(texture0, UV);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    UV = vUV;
    return vec4(VertexPosition, 1.0);
}
#endif