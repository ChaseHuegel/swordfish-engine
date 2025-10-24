#version 330 core

#ifdef VERTEX
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec2 aTexCoords;
#endif

inout vec2 TexCoords;

uniform sampler2D gPosition;
uniform sampler2D gNormal;
uniform sampler2D gColor;

uniform vec3 viewLightDirection = vec3(0, 1, -0.25);
uniform vec3 globalLightPosition = vec3(0, 1, -0.25);
uniform vec3 globalLightColor = vec3(1, 1, 1);

uniform float ambientLightning = 0.1;
uniform vec3 ambientLightColor = vec3(1, 1, 1);

vec4 vertex()
{
    TexCoords = aTexCoords;
    return vec4(aPos, 1.0);
}

vec4 fragment()
{
    vec3 pos = texture(gPosition, TexCoords).rgb;
    vec3 normal = texture(gNormal, TexCoords).rgb;
    normal = normalize(normal * 2.0 - 1.0);
    vec4 color = texture(gColor, TexCoords);
    float specular = 1;
    
    vec3 lightDir = normalize(-viewLightDirection);

    float directionalValue = max(0.0, dot(normal, lightDir));
    vec3 diffuse = directionalValue * globalLightColor;

    vec3 ambientLight = ambientLightning * ambientLightColor;

    vec3 lightValue = min(ambientLight + diffuse, vec3(1));
    vec4 lighting = vec4(lightValue, 1.0);

    vec3 viewDir  = normalize(-pos);
    vec3 halfwayDir = normalize(lightDir + viewDir);  
    vec3 reflectDir = reflect(-lightDir, normal);
    float spec = pow(max(dot(reflectDir, viewDir), 0.0), 16.0);
    vec4 specColor = vec4(globalLightColor * spec * specular, 1.0);

    return (color * lighting) + specColor;
}