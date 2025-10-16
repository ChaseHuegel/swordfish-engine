#version 430 core
#include "pbr_plus.glsl"

#ifdef FRAGMENT
uniform sampler2DArray texture0;

vec4 fragment()
{
    vec4 texSample = texture(texture0, TextureCoord);

    //  Discard transparent fragments
    if (texSample.a == 0)
        discard;

    return texSample * VertexColor * shade();
}
#endif