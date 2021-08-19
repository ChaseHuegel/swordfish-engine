#version 330 core
out vec4 FragColor;

in vec2 uv;

uniform sampler2D texture0;

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

float mapRange(float value, float oldMin, float oldMax, float newMin, float newMax)
{
    return (((value - oldMin) * (newMax - newMin)) / (oldMax - oldMin)) + newMin;
}

void main()
{
    vec3 color = texture(texture0, uv).rgb;

    //  HDR
    // color = color / (color + vec3(1.0));

    //  Gamma correction
    color = pow(color, vec3(1.0/2.2));

    //  Dithering
    float grayscale = dot(color.rgb, vec3(0.299, 0.587, 0.114));
    if (grayscale < 0.23)
        color *= dither( mapRange(grayscale, 0, 0.23, 0, 1) );

    FragColor = vec4(color, 1);
}