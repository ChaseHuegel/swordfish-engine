#version 330 core

const float PI = 3.14159265359;

inout vec3 FragPos;

uniform vec3 viewPosition;

uniform float ambientLightning = 0.1;

uniform sampler2D texture0;

uniform float Metallic = 0.5;
uniform float Roughness = 0.5;

uniform int lightCount;
uniform vec3 lightPositions[4];
uniform vec3 lightColors[4];

vec4 vertex()
{
    FragPos = vec3(vec4(VertexPosition, 1) * model);
    return projection * view * model * vec4(VertexPosition, 1.0);
}

vec4 fragment()
{
    vec4 texSample = texture(texture0, TextureCoord.xy);

    //  Discard transparent fragments
    if (texSample.a == 0)
        discard;

    vec3 normal = mat3(inverse(model)) * VertexNormal;
    vec3 viewDir = normalize(viewPosition - FragPos);

    //  Sample albedo
    vec3 albedo = texSample.rgb;

    //  Reflectance based on metallicness
    //  0.04 at full plastic and albedo at full Metallic
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, Metallic);

    //  Per-light radiance calculation
    vec3 totalRadiance = vec3(0.0);
    for(int i = 0; i < lightCount; ++i)
    {
        if (lightColors[i].r == 0 && lightColors[i].g == 0 && lightColors[i].b == 0)
            continue;

        vec3 L = normalize(lightPositions[i] - FragPos);
        vec3 H = normalize(viewDir + L);

        float distance = length(lightPositions[i] - FragPos);
        float attenuation = 1 / (1.0 + 0.7 * distance + 1.8 * pow(distance, 2));
        vec3 radiance = lightColors[i] * attenuation;

        //  Cook-Torrance BRDF
        float NDF = DistributionGGX(VertexNormal, H, Roughness);
        float G = GeometrySmith(VertexNormal, viewDir, L, Roughness);
        vec3 F = fresnelSchlick(clamp(dot(H, viewDir), 0.0, 1.0), F0);

        vec3 numerator    = NDF * G * F;
        float denominator = 4 * max(dot(VertexNormal, viewDir), 0.0) * max(dot(VertexNormal, L), 0.0);
        vec3 specular = numerator / denominator;

        //  Energy consevation; diffuse + specular can't go over 1.0 unless light emitting
        vec3 kD = vec3(1.0) - F;

        //  Metallic surfaces have no diffuse lightning, blends linearly to non-Metallic
        kD *= 1.0 - Metallic;

        //  Shading
        float NdotL = max(dot(VertexNormal, L), 0.0);

        totalRadiance += (kD * albedo / PI + specular) * radiance * NdotL;
    }

    float occlusion = 1;

    //  Ambient light
    vec3 ambient = albedo * ambientLightning * occlusion;

    //  Final color
    vec3 color = ambient + totalRadiance;

    //  HDR
    // color = color / (color + vec3(1.0));

    return vec4(color, texSample.a) * VertexColor;
}

float DistributionGGX(vec3 Normal, vec3 H, float Roughness)
{
    float a = Roughness * Roughness;
    float a2 = a * a;
    float NdotH = max(dot(Normal, H), 0.0);
    float NdotH2 = NdotH * NdotH;

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