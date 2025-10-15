#version 430 core

inout vec3 vWorldPos;
inout vec3 vNormal;
inout vec3 vViewPos;

#ifdef FRAGMENT
struct Light
{
    vec4 pos_radius;      // view-space pos.xyz, radius in w
    vec4 color_intensity; // rgb, intensity in w
};

layout(std430, binding = 0) buffer LightBuffer
{
    Light lights[];
};

layout(std430, binding = 1) buffer TileLightIndices
{
    uint indices[];
};

layout(std430, binding = 2) buffer TileCounts
{
    uint counts[];
};

uniform ivec2 uScreenSize;
uniform ivec2 uTileSize;
uniform int uMaxLightsPerTile;

vec3 EvalLight(Light L, vec3 posView, vec3 normal)
{
    vec3 lightPos = L.pos_radius.xyz;
    float radius = L.pos_radius.w;
    vec3 toLight = lightPos - posView;
    float dist = length(toLight);
    vec3 Ldir = normalize(toLight);
    float NdotL = max(dot(normal, Ldir), 0.0);
    float att = clamp(1.0 - (dist / radius), 0.0, 1.0);
    vec3 col = L.color_intensity.rgb * L.color_intensity.w;
    return col * NdotL;
}

vec4 fragment()
{
    ivec2 pix = ivec2(gl_FragCoord.xy);
    ivec2 tile = pix / uTileSize;
    int tilesX = (uScreenSize.x + uTileSize.x - 1) / uTileSize.x;
    int tId = tile.y * tilesX + tile.x;

    uint count = counts[tId];
    uint base = uint(tId) * uint(uMaxLightsPerTile);

    vec3 posView = vViewPos;
    vec3 normal = normalize(vNormal);

    vec3 color = vec3(0.0);
    for (uint i = 0u; i < count; ++i) {
        uint li = indices[base + i];
        Light L = lights[li];
        color += EvalLight(L, posView, normal);
    }

    return vec4(color, 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    vec4 worldPos = model * vec4(VertexPosition, 1.0);
    vWorldPos = worldPos.xyz;

    vNormal = mat3(model) * VertexNormal;

    vec4 viewPos = view * worldPos;
    vViewPos = viewPos.xyz;

    return projection * viewPos;
}
#endif