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

const float PI = 3.14159265359;

uniform ivec2 uScreenSize;
uniform ivec2 uTileSize;
uniform int uMaxLightsPerTile;

uniform vec3 viewPosition;
uniform float ambientLightning = 0.5;
uniform float Metallic = 0.5;
uniform float Roughness = 0.5;

float DistributionGGX(vec3 Normal, vec3 H, float Roughness)
{
    float a = Roughness*Roughness;
    float a2 = a*a;
    float NdotH = max(dot(Normal, H), 0.0);
    float NdotH2 = NdotH*NdotH;

    float nom = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}

float GeometrySchlickGGX(float NdotV, float Roughness)
{
    float r = (Roughness + 1.0);
    float k = (r*r) / 8.0;

    float nom = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

float GeometrySmith(vec3 Normal, vec3 viewDir, vec3 L, float Roughness)
{
    float NdotV = max(dot(Normal, viewDir), 0.0);
    float NdotL = max(dot(Normal, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, Roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, Roughness);

    return ggx1 * ggx2;
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(max(1.0 - cosTheta, 0.0), 5.0);
}

vec3 EvalLight(Light light)
{
    vec3 viewDir = normalize(viewPosition - vWorldPos);

    //  Sample albedo
    vec3 albedo = vec3(1);

    float occlusion = 1;

    //  Reflectance based on metallicness
    //  0.04 at full plastic and albedo at full Metallic
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, Metallic);

    //  Per-light radiance calculation
    vec3 totalRadiance = vec3(0.0);

    vec3 lightPos = light.pos_radius.xyz;
    vec3 lightColor = light.color_intensity.rgb;
    float lightIntensity = light.color_intensity.w;

    vec3 L = normalize(lightPos - vWorldPos);
    vec3 H = normalize(viewDir + L);

    float distance = length(lightPos - vWorldPos);
    float attenuation = 1 / (1.0 + 0.7 * distance + 1.8 * pow(distance, 2));
    vec3 radiance = lightColor * attenuation * lightIntensity;

    //  Cook-Torrance BRDF
    float NDF = DistributionGGX(vNormal, H, Roughness);
    float G = GeometrySmith(vNormal, viewDir, L, Roughness);
    vec3 F = fresnelSchlick(clamp(dot(H, viewDir), 0.0, 1.0), F0);

    vec3 numerator    = NDF * G * F;
    float denominator = max(4.0 * max(dot(vNormal, viewDir), 0.0) * max(dot(vNormal, L), 0.0), 0.001);
    vec3 specular = numerator / denominator;

    //  Energy consevation; diffuse + specular can't go over 1.0 unless light emitting
    vec3 kD = vec3(1.0) - F;

    //  Metallic surfaces have no diffuse lightning, blends linearly to non-Metallic
    kD *= 1.0 - Metallic;

    //  Shading
    float NdotL = max(dot(vNormal, L), 0.0);

    totalRadiance += (kD * albedo / PI + specular) * radiance * NdotL;

    //  Final color
    vec3 color = totalRadiance * occlusion;

    //  HDR
    // color = color / (color + vec3(1.0));

    //  Output
    return color;
}

vec4 fragment()
{
    ivec2 pix = ivec2(gl_FragCoord.xy);
    ivec2 tile = pix / uTileSize;
    int tilesX = (uScreenSize.x + uTileSize.x - 1) / uTileSize.x;
    int tId = tile.y * tilesX + tile.x;

    if (tId >= counts.length()) {
        return vec4(0, 1, 0, 1);    
    }

    uint count = counts[tId];
    count = min(count, uint(uMaxLightsPerTile));
    uint base = uint(tId) * uint(uMaxLightsPerTile);

    vec3 color = vec3(0);
    for (uint i = 0u; i < count; ++i) {
        uint indexPos = base + i;
        if (indexPos >= indices.length())
            break;

        uint li = indices[base + i];
        if (li < lights.length()) {
            Light L = lights[li];
            color += EvalLight(L);
        }
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