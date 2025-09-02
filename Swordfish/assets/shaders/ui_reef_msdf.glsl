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
float median(float a, float b, float c);

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

    vec2 atlasDimensions = textureSize(texture0, 0);
    vec2 uv = vec2(TextureCoord.x / atlasDimensions.x, TextureCoord.y / atlasDimensions.y);
    vec4 sample = texture(texture0, uv);
    float distance = median(sample.r, sample.g, sample.b) - 0.5;
    float alpha = clamp(distance / fwidth(distance) + 0.5, 0.0, 1.0);
    return mix(vec4(0), VertexColor, alpha);
}

float median(float a, float b, float c) {
    return max(min(a, b), min(max(a, b), c));
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