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

const int SAMPLES_PER_TILE = 3;

uniform ivec2 uScreenSize;
uniform ivec2 uTileSize;
uniform int uNumLights;
uniform int uMaxLightsPerTile;
uniform float uMaxLightViewDistance;
uniform mat4 uInvProj; // inverse projection matrix
layout(binding = 0) uniform sampler2D uDepthTex;

ivec2 tileCoord() { return ivec2(gl_WorkGroupID.xy); }

int tileIndex(ivec2 t, int tilesX) { return t.y * tilesX + t.x; }

vec3 UnprojectToView(vec2 pixel, float depth) {
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

    ivec2 start = tile * uTileSize;
    ivec2 end = min(start + uTileSize, uScreenSize);

    int stepsX = max(1, (end.x - start.x) / SAMPLES_PER_TILE);
    int stepsY = max(1, (end.y - start.y) / SAMPLES_PER_TILE);
    
    float minD = 1.0;
    float maxD = 0.0;
    
    for (int oy = 0; oy <= end.y - start.y; oy += stepsY) {
        for (int ox = 0; ox <= end.x - start.x; ox += stepsX) {
            ivec2 p = start + ivec2(ox, oy);
            if (p.x >= uScreenSize.x || p.y >= uScreenSize.y) continue;
            float d = texelFetch(uDepthTex, p, 0).r;
            minD = min(minD, d);
            maxD = max(maxD, d);
        }
    }
    
    vec3 corners[4];
    corners[0] = UnprojectToView(vec2(start.x, start.y), minD);
    corners[1] = UnprojectToView(vec2(end.x, start.y), minD);
    corners[2] = UnprojectToView(vec2(start.x, end.y), maxD);
    corners[3] = UnprojectToView(vec2(end.x, end.y), maxD);
    
    vec3 tileMin = corners[0];
    vec3 tileMax = corners[0];
    for (int i = 1; i < 4; ++i) {
        tileMin = min(tileMin, corners[i]);
        tileMax = max(tileMax, corners[i]);
    }

    uint base = uint(tId) * uint(uMaxLightsPerTile);
    uint count = 0u;
    for (int i = 0; i < uNumLights; ++i) {
        vec4 lp = lights[i].pos_radius;
        vec3 lPos = lp.xyz;
        float radius = lp.w;
    
        vec3 closest = clamp(lPos, tileMin, tileMax);
        float dist2 = dot(closest - lPos, closest - lPos);
    
        if (dist2 <= uMaxLightViewDistance * uMaxLightViewDistance) {
            if (count < uint(uMaxLightsPerTile) && (base + count) < indices.length()) {
                indices[base + count] = uint(i);
                count += 1u;
            }
        }
    }
    
    counts[tId] = count;
}