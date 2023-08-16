#version 330 core

inout vec3 normal;

uniform sampler2D texture0;

uniform vec3 globalLightDirection = vec3(30, 45, 0);
uniform vec4 globalLightColor = vec4(1, 1, 1, 1);

uniform float ambientLightning = 0.1;
uniform vec4 ambientLightColor = vec4(1, 1, 1, 1);

vec4 vertex()
{
    normal = mat3(inverse(model)) * VertexNormal;
    return projection * view * model * vec4(VertexPosition, 1.0);
}

vec4 fragment()
{
    vec4 texSample = texture(texture0, TextureCoord.xy);

    //  Discard transparent fragments
    if (texSample.a == 0)
        discard;

    float directionalValue = max(dot(normal, normalize(globalLightDirection)), 0.0);
    vec4 diffuse = directionalValue * globalLightColor;

    vec4 ambientLight = ambientLightning * ambientLightColor;
    ambientLight.a = 1;

    return texSample * (ambientLight + diffuse) * VertexColor;
}