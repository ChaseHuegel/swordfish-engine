#version 430 core
#include "pbr_plus.glsl"

#ifdef FRAGMENT
uniform sampler2DArray texture0;

vec4 fragment()
{
    return texture(texture0, TextureCoord) * VertexColor * shade();
}
#endif