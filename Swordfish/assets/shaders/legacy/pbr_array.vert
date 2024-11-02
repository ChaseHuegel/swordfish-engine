#version 330 core
in vec3 in_position;
in vec3 in_normal;
in vec4 in_color;
in vec3 in_uv;

out vec4 Color;
out vec3 UV;
out vec3 Normal;
out vec3 FragPos;

uniform mat4 transform;
uniform mat4 inversedTransform;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    gl_Position = vec4(in_position, 1.0) * transform * view * projection;

    Color = in_color;
    UV = in_uv;

    Normal = mat3(inversedTransform) * in_normal;

    FragPos = vec3(vec4(in_position, 1) * transform);
}