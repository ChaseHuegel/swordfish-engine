#version 430

layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

struct Light {
    vec4 pos_radius;      // view-space position.xyz, radius in w
    vec4 color_intensity; // rgb, intensity in w
};
layout(std430, binding = 0) buffer LightBuffer {
    Light lights[];
};

layout(std430, binding = 1) buffer TileLightIndices {
    uint indices[]; // flattened: numTiles * MAX_LIGHTS_PER_TILE entries
};

layout(std430, binding = 2) buffer TileCounts {
    uint counts[]; // one uint per tile
};

uniform ivec2 uScreenSize;
uniform ivec2 uTileSize;
uniform int uNumLights;
uniform int uMaxLightsPerTile;
uniform mat4 uInvProj; // inverse projection matrix
layout(binding = 0) uniform sampler2D uDepthTex;

ivec2 tileCoord() { return ivec2(gl_WorkGroupID.xy); }

int tileIndex(ivec2 t, int tilesX) { return t.y * tilesX + t.x; }

vec3 UnprojectToView(vec2 pixel, float depth) {
    // pixel in screen coords [0..screen]
    vec2 ndc = (pixel / vec2(uScreenSize)) * 2.0 - 1.0;
    vec4 clip = vec4(ndc, depth * 2.0 - 1.0, 1.0); // convert [0..1] depth to clip z [-1..1]
    vec4 view = uInvProj * clip;
    view /= view.w;
    return view.xyz;
}

void compute() {
    ivec2 tile = tileCoord();
    int tilesX = (uScreenSize.x + uTileSize.x - 1) / uTileSize.x;
    int tilesY = (uScreenSize.y + uTileSize.y - 1) / uTileSize.y;
    if (tile.x >= tilesX || tile.y >= tilesY) return;
    int tId = tileIndex(tile, tilesX);

    // compute tile center pixel
    ivec2 start = tile * uTileSize;
    ivec2 centerPixel = start + uTileSize / 2;

    // sample min depth for tile (cheap: sample 4 corners)
    float minD = 1.0;
    for (int oy = 0; oy <= uTileSize.y; oy += max(1, uTileSize.y - 1)) {
        for (int ox = 0; ox <= uTileSize.x; ox += max(1, uTileSize.x - 1)) {
            ivec2 p = start + ivec2(ox, oy);
            if (p.x >= uScreenSize.x || p.y >= uScreenSize.y) continue;
            float d = texelFetch(uDepthTex, p, 0).r;
            minD = min(minD, d);
        }
    }

    // reconstruct a view-space point for the tile (conservative using minD)
    vec3 tileViewPos = UnprojectToView(vec2(centerPixel), minD);

    // iterate lights (lights are expected to be in view-space)
    uint base = uint(tId) * uint(uMaxLightsPerTile);
    uint count = 0u;
    for (int i = 0; i < uNumLights; ++i) {
        vec4 lp = lights[i].pos_radius;
        float radius = lp.w;
        // distance test in view-space:
        // distance between tileViewPos and light position <= radius + some slack (conservative)
        float d2 = dot(tileViewPos - lp.xyz, tileViewPos - lp.xyz);
        if (d2 <= (radius * radius)) {
            if (count < uint(uMaxLightsPerTile)) {
                indices[base + count] = uint(i);
                count += 1u;
            }
        }
    }
    counts[tId] = count;
}