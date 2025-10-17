#version 430 core
#include "pbr_plus.glsl"

#ifdef FRAGMENT
uniform sampler2DArray texture0;
uniform sampler2DArray texture1;
uniform sampler2DArray texture2;
uniform sampler2DArray texture3;
uniform sampler2DArray texture4;

vec4 fragment()
{
    vec4 diffuse = texture(texture0, TextureCoord);
    vec4 emissive = texture(texture4, TextureCoord);
    return diffuse * VertexColor * shade() + emissive;
}
#endif