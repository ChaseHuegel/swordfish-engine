#version 330 core
layout (location = 0) out vec4 FragColor;
layout (location = 1) out vec4 HdrColor;

in vec2 uv;

uniform sampler2D _Diffuse;

uniform vec4 BackgroundColor = vec4(1);
uniform float Exposure = 1;

const float offset = 1.0 / 300.0;

float mapRange(float value, float oldMin, float oldMax, float newMin, float newMax)
{
    return (((value - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
}

//  -----------------------------------------------------
//  --- Dithering on the GPU by Alex Charlton ---
//  http://alex-charlton.com/posts/Dithering_on_the_GPU/
const int indexMatrix4x4[16] = int[](0,  8,  2,  10,
                                     12, 4,  14, 6,
                                     3,  11, 1,  9,
                                     15, 7,  13, 5);

float indexValue()
{
    int x = int(mod(gl_FragCoord.x, 4));
    int y = int(mod(gl_FragCoord.y, 4));
    return indexMatrix4x4[(x + y * 4)] / 16.0;
}

float dither(float color)
{
    float closestColor = (color < 0.5) ? 0 : 1;
    float secondClosestColor = 1 - closestColor;
    float d = indexValue();
    float distance = abs(closestColor - color);
    return (distance < d) ? closestColor : secondClosestColor;
}
//  -----------------------------------------------------

float sampleLuminance(sampler2D _tex, int samples)
{
    vec3 average = vec3(1);

    for (int x = 0; x < samples; x++)
    {
        for (int y = 0; y < samples; y++)
        {
            vec2 pos = vec2( textureSize(_tex, 0).x/samples*x, textureSize(_tex, 0).y/samples*y );

            if (texture(_tex, pos).a != 0)
                average += texture(_tex, pos).rgb;
            else
                average += vec3(1);
        }
    }

    average /= samples;

    return 0.2126 * average.r + 0.7152 * average.g + 0.0722 * average.b;
}

float getLuminance(vec4 color)
{
    return 0.2126 * color.r + 0.7152 * color.g + 0.0722 * color.b;
}

void main()
{
    vec3 color = texture(_Diffuse, uv).rgb;
    float grayscale = dot(color, vec3(0.2126, 0.7152, 0.0722));

    bool isClearColor = (texture(_Diffuse, uv).a == 0);

    if (texture(_Diffuse, uv).a != 1)
        color = mix(pow(BackgroundColor.rgb, vec3(2.2)), color, texture(_Diffuse, uv).a);

    //  HDR
    color = vec3(1.0) - exp(-color * Exposure);

    grayscale = dot(color, vec3(0.2126, 0.7152, 0.0722));

    if (grayscale > 1)
        HdrColor = vec4(color, 1);
    else
        HdrColor = vec4(1, 0, 0, 1);

    //  Gamma correction
    color = pow(color, vec3(1.0/2.2));

    //  Dithering
    if (grayscale < 0.23 && texture(_Diffuse, uv).a != 0)
        color *= dither( mapRange(grayscale, 0, 0.23, 0, 1) );

    FragColor = vec4(color, 1);
}