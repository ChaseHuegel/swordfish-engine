#version 330 core

#ifdef VERTEX
layout (location = 3) in vec4 in_clipRect;
#endif

#ifdef FRAGMENT
layout(origin_upper_left) in vec4 gl_FragCoord;
#endif

inout vec4 ClipRect;

uniform sampler2D texture0;

float screenPxRange(vec2 uv, vec2 atlasSize);
float median(float a, float b, float c);
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

    vec2 atlasSize = textureSize(texture0, 0);
    vec2 uv = vec2(TextureCoord.x / atlasSize.x, 1.0 - (TextureCoord.y / atlasSize.y));

    vec4 sample = texture(texture0, uv);
    float signedDistance = median(sample.r, sample.g, sample.b);
    float screenPxDistance = screenPxRange(uv, atlasSize) * (signedDistance - 0.5);
    float alpha = clamp(screenPxDistance + 0.5, 0.0, 1.0);

    return mix(vec4(0), VertexColor, alpha);
}

float screenPxRange(vec2 uv, vec2 atlasSize) {
    vec2 unitRange = vec2(6.0) / vec2(atlasSize);
    const float fwidth = 0.1;
    vec2 screenTexSize = vec2(1.0) / fwidth;
    return max(0.5 * dot(unitRange, screenTexSize), 1.0);
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