#version 430 core

inout vec3 vWorldPos;
inout vec3 vNormal;
inout vec3 vViewPos;
vec3 Normal;
float Metallic;
float Roughness;

#ifdef FRAGMENT
struct Light
{
    vec4 pos_radius; // view-space pos.xyz, radius in w
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

uniform vec3 uCameraPos;
uniform vec3 uAmbientLight = vec3(0.05);
uniform float uMetallic = 0.5;
uniform float uRoughness = 0.5;

uniform sampler2D uAO;
uniform sampler2D uPreDepth;
uniform sampler2D uDepth;

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

vec3 EvalLight(Light light, vec3 N, vec3 V, vec3 F0)
{
    vec3 lightPos = light.pos_radius.xyz;
    float radius = light.pos_radius.w;
    vec3 lightColor = light.color_intensity.rgb;
    float intensity = light.color_intensity.w;
    
    // Radiance
    vec3 L = normalize(lightPos - vWorldPos);
    vec3 H = normalize(V + L);
    float dist = length(lightPos - vWorldPos);
    float attenuation = clamp(1.0 - (dist / radius) * (dist / radius), 0.0, 1.0);
    vec3 radiance = lightColor * attenuation * radius;
    
    // Cook-Torrance BRDF
    float NDF = DistributionGGX(N, H, Roughness);
    float G = GeometrySmith(N, V, L, Roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001;
    vec3 specular = numerator / denominator;
    
    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;
    kD *= 1.0 - Metallic;
    
    float NdotL = max(dot(N, L), 0.0);
    return (kD * vec3(1.0) / PI + specular) * radiance * NdotL;
}

vec4 shade()
{
    ivec2 pix = ivec2(gl_FragCoord.xy);
    ivec2 tile = pix / uTileSize;
    int tilesX = (uScreenSize.x + uTileSize.x - 1) / uTileSize.x;
    int tId = tile.y * tilesX + tile.x;

    uint count = counts[tId];
    count = min(count, uint(uMaxLightsPerTile));
    uint base = uint(tId) * uint(uMaxLightsPerTile);

    vec3 V = normalize(uCameraPos - vWorldPos);
    vec3 F0 = mix(vec3(0.04), vec3(1.0), Metallic);

    vec3 color = uAmbientLight;
    for (uint i = 0u; i < count; ++i) {
        uint indexPos = base + i;
        if (indexPos >= indices.length())
        {
            break;
        }

        uint lightIndex = indices[base + i];
        if (lightIndex < lights.length()) {
            Light light = lights[lightIndex];
            color += EvalLight(light, vNormal, V, F0);
        }
    }

    return vec4(color, 1.0);
}

vec4 ao(vec4 diffuse)
{
    vec2 screenUV = (gl_FragCoord.xy + 0.5) / uScreenSize;

    float fragDepth = texture(uDepth, screenUV).r;
    float aoDepth = texture(uPreDepth, screenUV).r;

    float visibility = step(aoDepth, fragDepth);
    
    float ao = mix(1.0, texture(uAO, screenUV).r, diffuse.a * visibility);
    return vec4(ao, ao, ao, 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    vec4 worldPos = model * vec4(VertexPosition, 1.0);
    vWorldPos = worldPos.xyz;

    vNormal = mat3(transpose(inverse(model))) * VertexNormal;

    return projection * view * worldPos;
}
#endif