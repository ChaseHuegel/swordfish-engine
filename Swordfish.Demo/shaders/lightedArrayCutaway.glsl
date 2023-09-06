#version 330 core

inout vec3 normal;
inout vec3 pos;
inout vec3 camPos;

uniform sampler2DArray texture0;

uniform vec3 globalLightPosition = vec3(0, 1, 0.25);
uniform vec3 globalLightColor = vec3(1, 1, 1);

uniform float ambientLightning = 0.5;
uniform vec3 ambientLightColor = vec3(1, 1, 1);

vec4 vertex()
{
    normal = mat3(inverse(model)) * VertexNormal;
    pos = VertexPosition;
    camPos = inverse(view)[3].xyz;
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

    float depth = gl_FragCoord.z / gl_FragCoord.w;
    float yTarget = round(camPos.y - 12);
    if (pos.y > yTarget)
        discard;

    vec4 col;
    if (gl_FrontFacing)
        col = texSample * lighting * VertexColor;
    else
        col = vec4(1);

    return col;
}