#version 330 core
const float PI = 3.14159265359;

out vec4 FragColor;

in vec3 FragPos;
in vec4 Color;
in vec2 UV;
in vec3 Normal;

uniform sampler2D texture0;

uniform vec3 viewPosition;

uniform vec3 globalLightDirection = vec3(30, 45, 0);
uniform vec4 globalLightColor = vec4(1, 1, 1, 1);

uniform float ambientLightning = 0.1;

uniform vec3 lightPositions[4];
uniform vec3 lightColors[4];

void main()
{
    //  Discard completely transparent fragments
    if (texture(texture0, UV).a == 0 || texture(texture0, UV).rgb == vec3(0, 0, 0))
        discard;

    //  Sample albedo
    vec3 albedo = texture(texture0, UV).rgb;

    vec3 diffuse = vec3(0.0);
    for(int i = 0; i < 4; ++i)
    {
        if (lightColors[i].r == 0 && lightColors[i].g == 0 && lightColors[i].b == 0)
            continue;

        float distance = length(lightPositions[i] - FragPos);
        // float attenuation = 1.0 / (1.0 + 0.09 * distance + 0.032 * (distance * distance));
        float attenuation = 1.0 / (distance*distance);

        float dVal = max( dot(Normal, normalize(lightPositions[i] - FragPos)), 0.0 );

        diffuse += dVal * albedo * Color.rgb * lightColors[i] * attenuation;
    }

    vec3 result = diffuse + (diffuse * ambientLightning);

    FragColor = vec4(result, texture(texture0, UV).a);
}