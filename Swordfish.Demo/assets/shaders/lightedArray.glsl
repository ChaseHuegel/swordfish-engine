#version 330 core

inout vec3 normal;

uniform sampler2DArray texture0;

uniform vec3 globalLightPosition = vec3(0, 1, 0.25);
uniform vec3 globalLightColor = vec3(1, 1, 1);

uniform float ambientLightning = 0.5;
uniform vec3 ambientLightColor = vec3(1, 1, 1);

vec4 vertex()
{
    normal = mat3(inverse(model)) * VertexNormal;
    return projection * view * model * vec4(VertexPosition, 1.0);
}

vec4 fragment()
{
    vec4 texSample = texture(texture0, TextureCoord);

    //  Discard transparent fragments
    if (texSample.a == 0)
        discard;

    vec3 surfaceToLight = normalize(globalLightPosition);
    float directionalValue = max(0.0, dot(normal, surfaceToLight));
    vec3 diffuse = directionalValue * globalLightColor;

    vec3 ambientLight = ambientLightning * ambientLightColor;

    vec3 lightValue = min(ambientLight + diffuse, vec3(1));
    vec4 lighting = vec4(lightValue, 1.0);

    return texSample * lighting * VertexColor;
}