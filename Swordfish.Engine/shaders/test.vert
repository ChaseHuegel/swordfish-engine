#version 330 core
in vec3 in_position;
in vec3 in_normal;
in vec4 in_color;
in vec2 in_uv;

out vec4 color;
out vec2 uv;
out vec3 normal;
out vec3 FragPos;

uniform mat4 transform;
uniform mat4 inversedTransform;
uniform mat4 view;
uniform mat4 projection;
uniform vec4 tint = vec4(1f, 1f, 1f, 1f);

void main()
{
    gl_Position = vec4(in_position, 1.0) * transform * view * projection;

    color = in_color * tint;
    uv = in_uv;

    normal = mat3(inversedTransform) * in_normal;

    FragPos = vec3(transform * vec4(in_position, 1));
}