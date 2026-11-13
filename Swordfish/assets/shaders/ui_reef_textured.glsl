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

vec3 linearToSrgb(vec3 x)
{
    x = clamp(x, 0.0, 1.0);
    vec3 lo = x * 12.92;
    vec3 hi = 1.055 * pow(x, vec3(1.0 / 2.4)) - 0.055;
    return mix(lo, hi, step(vec3(0.0031308), x));
}

vec4 fragment()
{
    if (!contains(ClipRect, gl_FragCoord.x - 0.5, gl_FragCoord.y - 0.5))
    {
        return vec4(0);
    }

    vec4 tex = texture(texture0, TextureCoord.xy);
    return VertexColor * vec4(linearToSrgb(tex.rgb), tex.a);
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
