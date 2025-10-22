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
    vec4 PosRadius; // view-space pos.xyz, radius in w
    vec4 ColorSize; // rgb, size in w
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

const int   g_sss_max_steps        = 16;
const float g_sss_ray_max_distance = 0.05;
const float g_sss_thickness        = 0.02;
const float g_sss_step_length      = g_sss_ray_max_distance / float(g_sss_max_steps);

uniform mat4 view;
uniform mat4 projection;

float screen_fade(vec2 uv)
{
    float edgeDist = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
    return clamp(edgeDist * 8.0, 0.0, 1.0); // soft fade within ~1/8 of screen border
}

float ScreenSpaceShadows(vec3 fragWorldPos, vec3 lightWorldPos)
{
    vec3 L = lightWorldPos - fragWorldPos;
    vec3 ray_pos = fragWorldPos;
    vec3 ray_dir = L;

    vec3 ray_step = ray_dir * g_sss_step_length;

    float occlusion = 0.0;
    vec3 ray_screenPos = vec3(0.0);

    for (int i = 0; i < g_sss_max_steps; ++i)
    {
        ray_pos += ray_step;

        vec4 rayClip = projection * view * vec4(ray_pos, 1.0);
        vec3 rayNDC = rayClip.xyz / rayClip.w;
        vec2 rayUV = rayNDC.xy * 0.5 + 0.5;

        if (all(greaterThanEqual(rayUV, vec2(0.0))) && all(lessThanEqual(rayUV, vec2(1.0))))
        {
            float depth = texture(uDepth, rayUV).r;
            float depthNDC = depth * 2.0 - 1.0;

            if (depthNDC < rayNDC.z - 0.001)
            {
                occlusion = 1.0;
                break;
            }
        }
    }

    return 1.0 - occlusion;
}

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

vec3 EvalLight(Light light, vec3 N, vec3 V, vec3 F0, vec3 albedo, float roughness, float metallic)
{
    vec3 lightPos = light.PosRadius.xyz;
    float radius = light.PosRadius.w;
    vec3 lightColor = light.ColorSize.rgb;
    float size = light.ColorSize.w;
    
    float dist = length(lightPos - vWorldPos);
    float attenuation = clamp(1.0 - (dist / radius) * (dist / radius), 0.0, 1.0);
    vec3 radiance = lightColor * attenuation * radius;

    vec3 L = normalize(lightPos - vWorldPos);
    float NdotL = max(dot(N, L), 0.0);
    vec3 lighting = (albedo / PI) * radiance * NdotL;

    float proximityRadiance = smoothstep(size, 0.0, dist);
    vec3 proximityLighting = lightColor * proximityRadiance;
    
    vec3 surface = mat3(view) * vWorldPos;
    vec3 lightDir = mat3(transpose(inverse(view))) * -L;
    float shadow = ScreenSpaceShadows(vWorldPos, lightPos);
    return vec3(shadow);//(lighting + proximityLighting) * shadow;
}

vec4 shade(vec3 albedo)
{
    ivec2 pix = ivec2(gl_FragCoord.xy);
    ivec2 tile = pix / uTileSize;
    int tilesX = (uScreenSize.x + uTileSize.x - 1) / uTileSize.x;
    int tId = tile.y * tilesX + tile.x;

    uint count = counts[tId];
    count = min(count, uint(uMaxLightsPerTile));
    uint base = uint(tId) * uint(uMaxLightsPerTile);

    vec3 V = normalize(uCameraPos - vWorldPos);
    vec3 F0 = mix(vec3(0.04), albedo, Metallic);

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
            color += EvalLight(light, vNormal, V, F0, albedo, Roughness, Metallic);
        }
    }

    return vec4(color, 1.0);
}

vec4 ao(vec4 albedo)
{
    vec2 screenUV = (gl_FragCoord.xy + 0.5) / uScreenSize;

    float fragDepth = texture(uDepth, screenUV).r;
    float aoDepth = texture(uPreDepth, screenUV).r;

    float visibility = step(aoDepth, fragDepth);
    
    float ao = mix(1.0, texture(uAO, screenUV).r, albedo.a * visibility);
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