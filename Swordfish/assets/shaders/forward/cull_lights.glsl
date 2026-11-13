#version 430

layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

struct Light {
    vec4 pos_radius; // xyz, radius
    vec4 color_intensity; // rgb, intensity
    vec4 shadow_params; // shadowIndex, bias, reserved, reserved
};

layout(std430, binding = 0) buffer LightBuffer {
    Light lights[];
};

layout(std430, binding = 1) buffer TileLightIndices {
    uint indices[];
};

layout(std430, binding = 2) buffer TileCounts {
    uint counts[];
};

uniform ivec2 uScreenSize;
uniform ivec2 uTileSize;
uniform int uNumLights;
uniform int uMaxLightsPerTile;
uniform mat4 uInvProj;
uniform mat4 uView;

layout(binding = 0) uniform sampler2D uDepthTex;

ivec2 GetTileCoord() 
{
    return ivec2(gl_WorkGroupID.xy);
}

int GetTileIndex(ivec2 t, int tilesX)
{ 
    return t.y * tilesX + t.x;
}

vec3 PixelToView(vec2 pixel, float depth)
{
    vec2 ndc = (pixel / vec2(uScreenSize)) * 2.0 - 1.0;
    vec4 clip = vec4(ndc, depth * 2.0 - 1.0, 1.0);
    vec4 view = uInvProj * clip;
    view /= view.w;
    return view.xyz;
}

void compute() 
{
    ivec2 tile = GetTileCoord();
    int tilesX = (uScreenSize.x + uTileSize.x - 1) / uTileSize.x;
    int tilesY = (uScreenSize.y + uTileSize.y - 1) / uTileSize.y;
    if (tile.x >= tilesX || tile.y >= tilesY) 
    {
        return;
    }
    
    int tId = GetTileIndex(tile, tilesX);

    ivec2 start = tile * uTileSize;
    ivec2 end = min(start + uTileSize, uScreenSize);
    
    float minD = 0.0;
    float maxD = 0.0;
    bool hasSky = false;
    
    for (int py = start.y; py < end.y; ++py) 
    {
        for (int px = start.x; px < end.x; ++px)
        {
            float d = texelFetch(uDepthTex, ivec2(px, py), 0).r;
            if (d >= 1.0)
            {
                hasSky = true;
            }
            else
            {
                maxD = max(maxD, d);
            }
        }
    }

    if (hasSky)
    {
        maxD = 1.0;
    }
    
    vec3 corners[8];
    corners[0] = PixelToView(vec2(start.x, start.y), minD);
    corners[1] = PixelToView(vec2(end.x, start.y), minD);
    corners[2] = PixelToView(vec2(start.x, end.y), minD);
    corners[3] = PixelToView(vec2(end.x, end.y), minD);
    corners[4] = PixelToView(vec2(start.x, start.y), maxD);
    corners[5] = PixelToView(vec2(end.x, start.y), maxD);
    corners[6] = PixelToView(vec2(start.x, end.y), maxD);
    corners[7] = PixelToView(vec2(end.x, end.y), maxD);
    
    vec3 tileMin = corners[0];
    vec3 tileMax = corners[0];
    for (int i = 1; i < 8; ++i)
    {
        tileMin = min(tileMin, corners[i]);
        tileMax = max(tileMax, corners[i]);
    }

    uint base = uint(tId) * uint(uMaxLightsPerTile);
    uint count = 0u;
    for (int i = 0; i < uNumLights; ++i)
    {
        vec4 lp = lights[i].pos_radius;
        vec3 lPos = (uView * vec4(lp.xyz, 1.0)).xyz;
        float radius = lp.w;
    
        vec3 closest = clamp(lPos, tileMin, tileMax);
        float dist2 = dot(closest - lPos, closest - lPos);
    
        if (dist2 <= radius * radius)
        {
            if (count < uint(uMaxLightsPerTile) && (base + count) < indices.length())
            {
                indices[base + count] = uint(i);
                count += 1u;
            }
        }
    }
    
    counts[tId] = count;
}