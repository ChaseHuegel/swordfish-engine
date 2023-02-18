#version 330 core
out vec4 FragColor;
in vec3 FragPos;

in vec4 color;
in vec2 uv;
in vec3 normal;

uniform sampler2D texture0;

uniform vec3 globalLightDirection = vec3(30, 45, 0);
uniform vec4 globalLightColor = vec4(1, 1, 1, 1);

uniform float ambientLightning = 0.1;
uniform vec4 ambientLightColor = vec4(1, 1, 1, 1);

void main()
{
    float dVal = max( dot(normal, normalize(globalLightDirection)), 0.0 );
    vec4 diffuse = dVal * globalLightColor;

    vec4 ambientLight = ambientLightning * ambientLightColor;
    ambientLight.a = 1;

    FragColor = color * texture(texture0, uv) * (ambientLight + diffuse);

    if (FragColor.a == 0)
        discard;
}