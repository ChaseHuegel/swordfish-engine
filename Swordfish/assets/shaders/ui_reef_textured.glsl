#version 330 core

#ifdef VERTEX
layout (location = 3) in vec4 in_clipRect;
#endif

#ifdef FRAGMENT
layout(origin_upper_left) in vec4 gl_FragCoord;
#endif

inout vec4 ClipRect;

uniform sampler2D texture0;

bool contains(vec4 rect, float x, float y);

vec4 vertex()
{
    ClipRect = in_clipRect;
    return vec4(VertexPosition, 1.0);
}

vec4 fragment()
{
    if (!contains(ClipRect, gl_FragCoord.x, gl_FragCoord.y))
    {
        discard;
    }

    return VertexColor * texture(texture0, TextureCoord.xy);
}

bool contains(vec4 rect, float x, float y)
{
    if (x > rect.z)
    {
        return false;
    }
    
    if (x < rect.x)
    {
        return false;
    }
    
    if (y > rect.w)
    {
        return false;
    }
    
    if (y < rect.y)
    {
        return false;
    }

    return true;
}