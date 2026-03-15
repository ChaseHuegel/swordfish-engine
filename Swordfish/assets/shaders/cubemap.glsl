#version 430

inout vec3 textureDirection;

uniform samplerCube cubemap;

#ifdef FRAGMENT
vec4 fragment()
{
    return texture(cubemap, textureDirection);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    textureDirection = VertexPosition;
    return projection * view * vec4(VertexPosition, 1.0);
}
#endif