#version 430 core
#include "pbr_plus.glsl"

#ifdef FRAGMENT
uniform sampler2DArray texture0;
uniform sampler2DArray texture1;
uniform sampler2DArray texture2;
uniform sampler2DArray texture3;
uniform sampler2DArray texture4;

vec4 fragment()
{
    vec4 diffuse = texture(texture0, TextureCoord);
    Metallic = texture(texture1, TextureCoord).r;
    Roughness = 1.0 - texture(texture2, TextureCoord).r;
    Normal = vNormal * (texture(texture3, TextureCoord).rgb * 2.0 - 1.0);
    vec4 emissive = texture(texture4, TextureCoord);
    vec4 color = max(diffuse * VertexColor * shade(), emissive);
    
    if (emissive.xyz != vec3(0))
    {
        BrightColor = emissive;
    }
    else
    {
        float brightness = dot(color.rgb, vec3(0.2126, 0.7152, 0.0722));
        if (brightness > 1.0)
        {
            BrightColor = vec4(color.rgb, 1.0);
        }
        else
        {
            BrightColor = vec4(0.0, 0.0, 0.0, 1.0);
        }
    }
        
    return color;
}
#endif