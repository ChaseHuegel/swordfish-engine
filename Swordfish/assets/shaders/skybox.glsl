#version 430

uniform vec3 uRGB;

#ifdef FRAGMENT
vec4 fragment()
{
    return vec4(uRGB, 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    return vec4(VertexPosition, 1.0);
}
#endif