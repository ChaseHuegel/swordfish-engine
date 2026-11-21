#version 430 core

inout vec3 vWorldPos;
inout vec3 vNormal;

#ifdef VERTEX
vec4 vertex()
{
    vec4 worldPos = model * vec4(VertexPosition, 1.0);
    vWorldPos = worldPos.xyz;
    vNormal = normalize(mat3(model) * VertexNormal);
    return projection * view * worldPos;
}
#endif

#ifdef FRAGMENT
uniform sampler2DArray texture0;
uniform sampler2DArray texture1;
uniform sampler2DArray texture2;
uniform sampler2DArray texture3;
uniform sampler2DArray texture4;

uniform float uAlphaCutoff = 0.0;

const float PI = 3.14159265359;
const vec3 KEY_LIGHT_COLOR = vec3(1.0, 0.98, 0.95) * 2.4;
const vec3 FILL_LIGHT_COLOR = vec3(0.85, 0.92, 1.0) * 0.75;

vec3 getNormal()
{
    vec3 N = normalize(vNormal);
    if (!gl_FrontFacing)
    {
        N = -N;
    }

    vec3 tangentNormal = texture(texture3, TextureCoord).xyz * 2.0 - 1.0;

    vec3 Q1 = dFdx(vWorldPos);
    vec3 Q2 = dFdy(vWorldPos);
    vec2 st1 = dFdx(TextureCoord.xy);
    vec2 st2 = dFdy(TextureCoord.xy);

    vec3 T = Q1 * st2.t - Q2 * st1.t;
    T = T - N * dot(N, T);
    float tLenSq = dot(T, T);
    if (tLenSq < 1e-8)
    {
        return N;
    }

    T *= inversesqrt(tLenSq);
    vec3 B = -normalize(cross(N, T));
    mat3 TBN = mat3(T, B, N);
    return normalize(TBN * tangentNormal);
}

float distributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    float denom = NdotH2 * (a2 - 1.0) + 1.0;
    return a2 / max(PI * denom * denom, 1e-5);
}

float geometrySchlickGGX(float NdotV, float roughness)
{
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;
    return NdotV / max(NdotV * (1.0 - k) + k, 1e-5);
}

float geometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    return geometrySchlickGGX(NdotV, roughness) * geometrySchlickGGX(NdotL, roughness);
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 evalDirectionalLight(vec3 L, vec3 radiance, vec3 N, vec3 V, vec3 F0, vec3 albedo, float roughness, float metallic)
{
    float NdotL = max(dot(N, L), 0.0);
    if (NdotL <= 0.0)
    {
        return vec3(0.0);
    }

    vec3 H = normalize(V + L);
    float NDF = distributionGGX(N, H, roughness);
    float G = geometrySmith(N, V, L, roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 kD = (vec3(1.0) - F) * (1.0 - metallic);
    vec3 specular = (NDF * G * F) / max(4.0 * max(dot(N, V), 0.0) * NdotL, 0.001);
    return (kD * albedo / PI + specular) * radiance * NdotL;
}

vec3 linearToSrgb(vec3 x)
{
    x = clamp(x, 0.0, 1.0);
    vec3 lo = x * 12.92;
    vec3 hi = 1.055 * pow(x, vec3(1.0 / 2.4)) - 0.055;
    return mix(lo, hi, step(vec3(0.0031308), x));
}

vec4 fragment()
{
    vec4 albedo = texture(texture0, TextureCoord);
    if (albedo.a < uAlphaCutoff)
    {
        discard;
    }

    float metallic = clamp(texture(texture1, TextureCoord).r, 0.0, 1.0);
    float roughness = clamp(1.0 - texture(texture2, TextureCoord).r, 0.08, 1.0);
    vec3 N = getNormal();
    vec3 emissive = texture(texture4, TextureCoord).rgb;

    vec3 viewDir = normalize(vec3(1.0, 0.85, 1.0));
    vec3 keyLightDir = normalize(vec3(0.45, 0.95, -0.70));
    vec3 fillLightDir = normalize(vec3(0.85, 0.25, 0.35));

    vec3 F0 = mix(vec3(0.04), albedo.rgb, metallic);
    vec3 lighting = evalDirectionalLight(keyLightDir, KEY_LIGHT_COLOR, N, viewDir, F0, albedo.rgb, roughness, metallic)
        + evalDirectionalLight(fillLightDir, FILL_LIGHT_COLOR, N, viewDir, F0, albedo.rgb, roughness, metallic);

    vec3 ambientColor = mix(vec3(0.18, 0.19, 0.22), vec3(0.36, 0.37, 0.40), N.y * 0.5 + 0.5);
    vec3 ambient = ambientColor * albedo.rgb * (1.0 - metallic * 0.45);

    vec3 linearColor = (ambient + lighting) * VertexColor.rgb + emissive;
    vec3 mapped = linearColor / (linearColor + vec3(1.0));
    return vec4(linearToSrgb(mapped), albedo.a);
}
#endif
