#version 430 core

in vec3 vWorldPos;
in vec3 vNormal;
in vec3 vViewPos; // view-space position if convenient (or compute from vWorldPos and view matrix)

struct Light {
    vec4 pos_radius;      // view-space pos.xyz, radius in w
    vec4 color_intensity; // rgb, intensity in w
};
layout(std430, binding = 0) buffer LightBuffer {
    Light lights[];
};
layout(std430, binding = 1) buffer TileLightIndices {
    uint indices[]; // flattened
};
layout(std430, binding = 2) buffer TileCounts {
    uint counts[];
};

uniform ivec2 uScreenSize;
uniform ivec2 uTileSize;
uniform int uMaxLightsPerTile;
uniform mat4 uView; // if needed to get view-space pos from world-space

vec3 EvalLight(Light L, vec3 posView, vec3 normal) {
    vec3 lightPos = L.pos_radius.xyz;
    float radius = L.pos_radius.w;
    vec3 toLight = lightPos - posView;
    float dist = length(toLight);
    vec3 Ldir = normalize(toLight);
    float NdotL = max(dot(normal, Ldir), 0.0);
    float att = clamp(1.0 - (dist / radius), 0.0, 1.0);
    vec3 col = L.color_intensity.rgb * L.color_intensity.w;
    return col * NdotL * att;
}

vec4 fragment() {
    // compute tile
    ivec2 pix = ivec2(gl_FragCoord.xy);
    ivec2 tile = pix / uTileSize;
    int tilesX = (uScreenSize.x + uTileSize.x - 1) / uTileSize.x;
    int tId = tile.y * tilesX + tile.x;

    uint count = counts[tId];
    uint base = uint(tId) * uint(uMaxLightsPerTile);

    // obtain view-space position and normal: assume vViewPos is view-space position (preferred)
    vec3 posView = vViewPos; // if not available: posView = (uView * vec4(vWorldPos,1)).xyz;
    vec3 normal = normalize(vNormal);

    vec3 color = vec3(0.0);
    for (uint i = 0u; i < count; ++i) {
        uint li = indices[base + i];
        Light L = lights[li];
        color += EvalLight(L, posView, normal);
    }

    // simple gamma-corrected output
    return vec4(color, 1.0);
}
