#version 330 core
const float PI = 3.14159265359;

out vec4 FragColor;

in vec4 Color;
in vec2 UV;
in vec3 Normal;
in vec3 FragPos;

uniform vec3 viewPosition;

uniform float ambientLightning = 0.1;

uniform sampler2D texture0;
uniform float metallic;
uniform float roughness;
uniform float ao;

uniform vec3 lightPositions[4];
uniform vec3 lightColors[4];
uniform float lightRanges[4];

float DistributionGGX(vec3 Normal, vec3 H, float roughness)
{
    float a = roughness*roughness;
    float a2 = a*a;
    float NdotH = max(dot(Normal, H), 0.0);
    float NdotH2 = NdotH*NdotH;

    float nom = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r*r) / 8.0;

    float nom = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

float GeometrySmith(vec3 Normal, vec3 viewDir, vec3 L, float roughness)
{
    float NdotV = max(dot(Normal, viewDir), 0.0);
    float NdotL = max(dot(Normal, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(max(1.0 - cosTheta, 0.0), 5.0);
}

void main()
{
    //  Discard completely transparent fragments
    if (texture(texture0, UV).a == 0)
        discard;

    vec3 viewDir = normalize(viewPosition - FragPos);

    //  Sample albedo
    vec3 albedo = texture(texture0, UV).rgb;

    //  Reflectance based on metallicness
    //  0.04 at full plastic and albedo at full metallic
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, metallic);

    //  Per-light radiance calculation
    vec3 totalRadiance = vec3(0.0);
    for(int i = 0; i < 4; ++i)
    {
        if (lightColors[i].r == 0 && lightColors[i].g == 0 && lightColors[i].b == 0)
            continue;

        vec3 L = normalize(lightPositions[i] - FragPos);
        vec3 H = normalize(viewDir + L);

        float distance = length(lightPositions[i] - FragPos);
        float attenuation = lightRanges[i] / (1.0 + 0.7 * distance + 1.8 * pow(distance, 2));
        if (attenuation < 0.0001) attenuation = 0;
        vec3 radiance = lightColors[i] * attenuation;

        //  Cook-Torrance BRDF
        float NDF = DistributionGGX(Normal, H, roughness);
        float G = GeometrySmith(Normal, viewDir, L, roughness);
        vec3 F = fresnelSchlick(clamp(dot(H, viewDir), 0.0, 1.0), F0);

        vec3 numerator    = NDF * G * F;
        float denominator = 4 * max(dot(Normal, viewDir), 0.0) * max(dot(Normal, L), 0.0);
        vec3 specular = numerator / denominator;

        //  Energy consevation; diffuse + specular can't go over 1.0 unless light emitting
        vec3 kD = vec3(1.0) - F;

        //  Metallic surfaces have no diffuse lightning, blends linearly to non-metallic
        kD *= 1.0 - metallic;

        //  Scale the light
        float NdotL = max(dot(Normal, L), 0.0);

        totalRadiance += (kD * albedo / PI + specular) * radiance * NdotL;
    }

    //  Ambient light
    vec3 ambient = vec3(0) * albedo * ao;

    //  Final color
    vec3 color = ambient + totalRadiance;

    //  HDR
    color = color / (color + vec3(1.0));

    //  Gamma correction
    color = pow(color, vec3(1.0/2.2));

    //  Output
    FragColor = vec4(color, texture(texture0, UV).a);
}