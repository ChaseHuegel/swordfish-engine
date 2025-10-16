#version 430 core
#include "pbr_plus.glsl"

#ifdef FRAGMENT
vec4 fragment()
{
    return shade();
}
#endif