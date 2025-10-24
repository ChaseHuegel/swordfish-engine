#version 330 core

#ifdef FRAGMENT
layout (location = 0) out vec4 gPosition;
layout (location = 1) out vec4 gNormal;
layout (location = 2) out vec4 gColor;
#endif

inout vec4 FragPos;
inout vec4 FragNormal;

uniform sampler2DArray texture0;

vec4 vertex()
{
    vec4 worldPos = model * vec4(VertexPosition, 1.0);
    vec4 viewPos = view * worldPos;
    FragPos = viewPos;
    
    mat3 normalMatrix = transpose(inverse(mat3(view * model)));
    FragNormal = vec4(normalize(normalMatrix * VertexNormal), 1.0);

    return projection * viewPos;
}

vec4 fragment()
{
    vec4 texSample = texture(texture0, TextureCoord);

    gPosition = FragPos;
    gNormal = FragNormal;
    gColor = texSample * VertexColor;

    return gColor;
}