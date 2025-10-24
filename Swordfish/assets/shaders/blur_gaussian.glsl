#version 430

#ifdef VERTEX
layout (location = 1) in vec2 vUV;
#endif

inout vec2 UV;

#ifdef FRAGMENT
const float WEIGHTS[5] = float[] (0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);

uniform sampler2D texture0;
uniform bool uHorizontal;

vec4 fragment()
{
    vec2 texelSize = 1.0 / textureSize(texture0, 0);
    vec3 result = texture(texture0, UV).rgb * WEIGHTS[0];
    if (uHorizontal)
    {
        for(int i = 1; i < 5; ++i)
        {
            vec2 offset = texelSize * i;
            result += texture(texture0, UV + vec2(offset.x * i, 0.0)).rgb * WEIGHTS[i];
            result += texture(texture0, UV - vec2(offset.x * i, 0.0)).rgb * WEIGHTS[i];
        }
    }
    else
    {
        for(int i = 1; i < 5; ++i)
        {
            vec2 offset = texelSize * i;
            result += texture(texture0, UV + vec2(0.0, offset.y * i)).rgb * WEIGHTS[i];
            result += texture(texture0, UV - vec2(0.0, offset.y * i)).rgb * WEIGHTS[i];
        }
    }
    
    return vec4(result, 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    UV = vUV;
    return vec4(VertexPosition, 1.0);
}
#endif