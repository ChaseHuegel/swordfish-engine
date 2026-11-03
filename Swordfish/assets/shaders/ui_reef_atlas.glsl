#version 330 core

#ifdef VERTEX
layout (location = 3) in vec4 in_clipRect;
layout (location = 8) in vec4 in_rect;
layout (location = 9) in vec4 in_uvRect;
#endif

#ifdef FRAGMENT
layout(origin_upper_left) in vec4 gl_FragCoord;
#endif

inout vec4 ClipRect;

uniform sampler2D texture0;

bool contains(vec4 rect, float x, float y);
float median(float a, float b, float c);
float screenPxRange(vec2 atlasSize);

vec4 vertex()
{
    //  0: bottom left, 1: bottom right, 2: top right, 3: top left.
    const vec2 CORNERS[4] = vec2[4](
        vec2(0.0, 0.0),
        vec2(1.0, 0.0),
        vec2(1.0, 1.0),
        vec2(0.0, 1.0)
    );

    vec2 corner = CORNERS[gl_VertexID];

    ClipRect = in_clipRect;
    VertexPosition = vec3(mix(in_rect.xy, in_rect.zw, corner), 0.0);
    TextureCoord = vec3(mix(in_uvRect.xy, in_uvRect.zw, corner), 0.0);

    return vec4(VertexPosition.xy, 0.0, 1.0);
}

vec4 fragment()
{
    if (!contains(ClipRect, gl_FragCoord.x - 0.5, gl_FragCoord.y - 0.5))
    {
        return vec4(0);
    }

    vec4 texel = texture(texture0, TextureCoord.xy);

    //  A solid white texel resolves to an alpha of one, so plain quads need no branch.
    float signedDistance = median(texel.r, texel.g, texel.b);
    float screenPxDistance = screenPxRange(vec2(textureSize(texture0, 0).xy)) * (signedDistance - 0.5);
    float alpha = clamp(screenPxDistance + 0.5, 0.0, 1.0);

    return VertexColor * alpha;
}

float screenPxRange(vec2 atlasSize)
{
    vec2 unitRange = vec2(6.0) / atlasSize;
    const float fwidth = 0.1;
    vec2 screenTexSize = vec2(1.0) / fwidth;
    return max(0.5 * dot(unitRange, screenTexSize), 1.0);
}

float median(float a, float b, float c)
{
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
