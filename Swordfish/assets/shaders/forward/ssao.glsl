#version 430

#ifdef VERTEX
layout (location = 1) in vec2 vUV;
#endif

inout vec2 UV;

uniform sampler2D uDepthTex;
uniform vec2 uViewScale;
uniform float uDepthA;
uniform float uDepthB;
uniform vec2 uRadiusScale;
uniform vec2 uDepthSize;

uniform float uRadius = 0.35;
uniform float uIntensity = 1.0;
uniform float uBias = 0.02;
uniform float uPower = 1.5;
uniform int uSampleCount = 8;
uniform float uMaxDistance = 48.0;
uniform float uFadeRange = 8.0;

const float EPSILON = 0.02;

#ifdef FRAGMENT

vec3 ViewPosition(vec2 uv, float depth)
{
    float ndcZ = depth * 2.0 - 1.0;
    float viewDistance = uDepthA / (uDepthB + ndcZ);
    vec2 ndc = uv * 2.0 - 1.0;
    return vec3(ndc * uViewScale * viewDistance, -viewDistance);
}

float SampleDepthBilinear(vec2 uv)
{
    //  Window depth is linear across a plane, so bilinear interpolation of the
    //  depth value keeps a sample on the surface. Manual taps reject any sky
    //  texel before blending so a silhouette sample never lands in mid-air.
    vec2 texel = uv * uDepthSize - 0.5;
    vec2 base = floor(texel);
    vec2 f = texel - base;

    ivec2 p = ivec2(base);
    ivec2 maxP = ivec2(uDepthSize) - 1;
    p = clamp(p, ivec2(0), maxP);
    ivec2 p1 = clamp(p + ivec2(1, 0), ivec2(0), maxP);
    ivec2 p2 = clamp(p + ivec2(0, 1), ivec2(0), maxP);
    ivec2 p3 = clamp(p + ivec2(1, 1), ivec2(0), maxP);

    float d00 = texelFetch(uDepthTex, p, 0).r;
    float d10 = texelFetch(uDepthTex, p1, 0).r;
    float d01 = texelFetch(uDepthTex, p2, 0).r;
    float d11 = texelFetch(uDepthTex, p3, 0).r;

    if (max(max(d00, d10), max(d01, d11)) >= 1.0)
    {
        return 1.0;
    }

    return mix(mix(d00, d10, f.x), mix(d01, d11, f.x), f.y);
}

vec2 GetNoise()
{
    //  White integer hash. Interleaved gradient noise repeats every ~15 px in
    //  x because of its inner 0.067 term, which puts a lattice into the AO
    //  rotation. A hash has no spatial period.
    uvec2 p = uvec2(gl_FragCoord.xy);

    uint a = p.x * 1597334677u ^ p.y * 3812015801u;
    a = (a ^ (a >> 16)) * 2246822519u;
    a = (a ^ (a >> 13)) * 3266489917u;
    a = a ^ (a >> 16);

    uint b = p.y * 1597334677u ^ p.x * 3812015801u;
    b = (b ^ (b >> 16)) * 2246822519u;
    b = (b ^ (b >> 13)) * 3266489917u;
    b = b ^ (b >> 16);

    return vec2(float(a), float(b)) / 4294967295.0 * 2.0 - 1.0;
}

bool ReconstructNormal(vec2 uv, vec3 centerPos, float centerDepth, out vec3 normal)
{
    vec2 invSize = 1.0 / uDepthSize;
    vec2 stepX = vec2(invSize.x, 0.0);
    vec2 stepY = vec2(0.0, invSize.y);

    float depthLeft = texture(uDepthTex, uv - stepX).r;
    float depthRight = texture(uDepthTex, uv + stepX).r;
    float depthDown = texture(uDepthTex, uv - stepY).r;
    float depthUp = texture(uDepthTex, uv + stepY).r;

    bool validLeft = depthLeft < 1.0;
    bool validRight = depthRight < 1.0;
    bool validDown = depthDown < 1.0;
    bool validUp = depthUp < 1.0;

    //  Each axis uses the neighbour that stays on this surface. A sky
    //  neighbour maps to the far plane, and a crease neighbour belongs to
    //  another face. Both have a larger depth delta than the same-surface
    //  side. On a grazing plane both sides recede by nearly the same amount,
    //  so either side still lies in the plane.
    vec3 tangentX;
    if (validLeft && validRight)
    {
        tangentX = abs(depthRight - centerDepth) <= abs(depthLeft - centerDepth)
            ? ViewPosition(uv + stepX, depthRight) - centerPos
            : centerPos - ViewPosition(uv - stepX, depthLeft);
    }
    else if (validRight)
    {
        tangentX = ViewPosition(uv + stepX, depthRight) - centerPos;
    }
    else if (validLeft)
    {
        tangentX = centerPos - ViewPosition(uv - stepX, depthLeft);
    }
    else
    {
        return false;
    }

    vec3 tangentY;
    if (validDown && validUp)
    {
        tangentY = abs(depthUp - centerDepth) <= abs(depthDown - centerDepth)
            ? ViewPosition(uv + stepY, depthUp) - centerPos
            : centerPos - ViewPosition(uv - stepY, depthDown);
    }
    else if (validUp)
    {
        tangentY = ViewPosition(uv + stepY, depthUp) - centerPos;
    }
    else if (validDown)
    {
        tangentY = centerPos - ViewPosition(uv - stepY, depthDown);
    }
    else
    {
        return false;
    }

    vec3 crossProduct = cross(normalize(tangentX), normalize(tangentY));
    float lengthSquared = dot(crossProduct, crossProduct);
    if (lengthSquared < 1e-8)
    {
        return false;
    }

    normal = crossProduct * inversesqrt(lengthSquared);
    return true;
}

float SAO(vec2 xy, vec3 verPos, vec3 n, vec2 noise, float radius)
{
    float g = 1.32471795724474602596; // plastic constant
    //  Perspective divide by view depth, not by the distance to the camera.
    //  The screen radius of a world-space offset scales with z. Using the
    //  Euclidean length shrinks the AO radius toward the screen corners.
    float viewDepth = -verPos.z;
    float radius2 = radius * radius;

    //  A non-positive sample count, or a zero or underflowed radius, would
    //  divide by zero. Return no occlusion instead of undefined math.
    if (uSampleCount <= 0 || radius2 <= 0.0)
    {
        return 1.0;
    }

    float occlusion = 0.0;
    for (int i = 0; i < uSampleCount; i++)
    {
        float angle = 6.2831853 * ((noise.x + float(i)) / float(uSampleCount));
        float radial = fract(noise.y + float(i) / g);

        vec2 nxy = xy + radial * radius * vec2(cos(angle), sin(angle)) * uRadiusScale / viewDepth;

        float sampleDepth = SampleDepthBilinear(nxy);
        if (sampleDepth >= 1.0)
        {
            continue;
        }

        vec3 samPos = ViewPosition(nxy, sampleDepth);
        vec3 tv = samPos - verPos;

        float vv = dot(tv, tv);
        float vn = dot(tv, n);

        //  Falloff and normal bias from the HPG12 estimator. The bias is a
        //  constant distance in metres, as in the reference implementation. A
        //  distance-scaled bias removes all occlusion on distant surfaces.
        float falloff = max(1.0 - vv / radius2, 0.0);
        falloff = falloff * falloff * falloff;

        occlusion += falloff * max(0.0, vn - uBias) / (vv + EPSILON);
    }

    //  pow(visibility, uPower). The base guard keeps pow(0, 0) and negative
    //  exponents defined, so any exponent is safe.
    float visibility = clamp(1.0 - uIntensity * occlusion / float(uSampleCount), 0.0, 1.0);
    return clamp(pow(max(visibility, 1e-9), uPower), 0.0, 1.0);
}

vec4 fragment()
{
    float depth = texture(uDepthTex, UV).r;
    if (depth >= 1.0)
    {
        return vec4(1.0);
    }

    vec3 posVS = ViewPosition(UV, depth);
    float viewDistance = length(posVS);
    if (viewDistance > uMaxDistance)
    {
        return vec4(1.0);
    }

    vec3 n;
    if (!ReconstructNormal(UV, posVS, depth, n))
    {
        return vec4(1.0);
    }

    vec2 noise = GetNoise();
    float ao = SAO(UV, posVS, n, noise, uRadius);

    //  A non-positive range is a hard cut at MaxDistance. smoothstep needs a
    //  positive interval, and the subtraction can collapse at very large
    //  MaxDistance, so both are checked.
    float fadeStart = uMaxDistance - uFadeRange;
    float fade = 1.0;
    if (uFadeRange > 0.0 && fadeStart < uMaxDistance)
    {
        fade = 1.0 - smoothstep(fadeStart, uMaxDistance, viewDistance);
    }

    ao = mix(1.0, ao, fade);
    return vec4(ao, ao, ao, 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    UV = vUV;
    return vec4(VertexPosition, 1.0);
}
#endif
