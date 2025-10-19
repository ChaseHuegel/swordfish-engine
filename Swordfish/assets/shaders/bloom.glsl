#version 430

#ifdef VERTEX
layout (location = 1) in vec2 vUV;
#endif

inout vec2 UV;

#ifdef FRAGMENT
uniform sampler2D texture0;
uniform sampler2D texture1;

vec4 fragment()
{
    //  Don't render any blur over emissive surfaces
    //  This is so bloom renders from edges only
    vec4 emissive = texture(texture1, UV);
    if (length(emissive.rgb) != 0)
    {
        discard;
    }
    
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