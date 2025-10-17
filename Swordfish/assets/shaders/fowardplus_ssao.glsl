#version 430

#ifdef VERTEX
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec2 aTexCoords;
#endif

inout vec2 TexCoords;

uniform sampler2D uDepthTex;
uniform ivec2     uScreenSize;
uniform mat4      uInvProj;
uniform int       aoMethod = 0;

uniform float uRadius = 0.5;     // base sampling radius
uniform float SIGMA   = 1.0;     // scaling factor for occlusion strength
uniform float SAO_K   = 1.0;     // exponent shaping
uniform int   SLICES  = 8;       // number of angular slices
uniform int   STEPS   = 4;       // number of radial steps

#ifdef FRAGMENT

const vec3 samples[16] = vec3[](
    vec3(0.5381, 0.1856, 0.4319),
    vec3(0.1379, 0.2486, 0.4430),
    vec3(0.3371, 0.5679, 0.0057),
    vec3(-0.6999, -0.0451, -0.0019),
    vec3(0.0689, -0.1598, -0.8547),
    vec3(0.0560, 0.0069, -0.1843),
    vec3(-0.0146, 0.1402, 0.0762),
    vec3(0.0100, -0.1924, -0.0344),
    vec3(-0.3577, -0.5301, -0.4358),
    vec3(-0.3169, 0.1063, 0.0158),
    vec3(0.0103, -0.5869, 0.0046),
    vec3(-0.0897, -0.4940, 0.3287),
    vec3(0.7119, -0.0154, -0.0918),
    vec3(-0.0533, 0.0596, -0.5411),
    vec3(0.0352, -0.0631, 0.5460),
    vec3(-0.4776, 0.2847, -0.0271)
);

const vec2 NOISE[16] = vec2[16](
    vec2( 0.897,  0.442),
    vec2(-0.784,  0.620),
    vec2( 0.221, -0.975),
    vec2(-0.991, -0.132),
    
    vec2( 0.540, -0.841),
    vec2( 0.998,  0.066),
    vec2(-0.414, -0.910),
    vec2( 0.123,  0.992),
    
    vec2(-0.665,  0.747),
    vec2( 0.777, -0.629),
    vec2(-0.992,  0.127),
    vec2( 0.306,  0.952),
    
    vec2(-0.222, -0.975),
    vec2( 0.885,  0.465),
    vec2(-0.556, -0.831),
    vec2( 0.031, -0.999)
);

const vec2 NOISE2[16] = vec2[16](
    vec2(-0.7374,  0.6755),
    vec2( 0.2325, -0.9726),
    vec2( 0.8870,  0.4617),
    vec2(-0.9931, -0.1174),

    vec2( 0.5299,  0.8480),
    vec2(-0.3546,  0.9350),
    vec2( 0.9990,  0.0452),
    vec2(-0.8035, -0.5953),

    vec2( 0.1179,  0.9930),
    vec2(-0.6747, -0.7381),
    vec2( 0.9511, -0.3090),
    vec2(-0.0835, -0.9965),

    vec2( 0.7071,  0.7071),
    vec2(-0.9135,  0.4067),
    vec2( 0.4067, -0.9135),
    vec2(-0.2249,  0.9744)
);

const vec2 NOISE3[256] = vec2[256](
    vec2(-0.6461,  0.7633), vec2( 0.1163, -0.9932), vec2( 0.3344,  0.9424), vec2( 0.7127, -0.7014),
    vec2(-0.8596, -0.5109), vec2(-0.9344, -0.3563), vec2( 0.7430,  0.6693), vec2( 0.2133,  0.9770),
    vec2( 0.5804,  0.8143), vec2( 0.5128, -0.8585), vec2( 0.8621, -0.5068), vec2(-0.8259, -0.5638),
    vec2( 0.6577, -0.7533), vec2(-0.5417, -0.8406), vec2(-0.9951, -0.0987), vec2(-0.8237,  0.5671),

    vec2(-0.9981,  0.0618), vec2( 0.9966,  0.0827), vec2(-0.9687, -0.2480), vec2( 0.6012, -0.7991),
    vec2(-0.9459, -0.3244), vec2(-0.1248, -0.9922), vec2(-0.2126,  0.9771), vec2(-0.8804, -0.4742),
    vec2( 0.1235,  0.9923), vec2( 0.5581, -0.8298), vec2(-0.9131, -0.4078), vec2(-0.3142, -0.9494),
    vec2(-0.6767, -0.7362), vec2(-0.9455,  0.3257), vec2(-0.9690, -0.2472), vec2( 0.5434, -0.8395),

    vec2( 0.7401, -0.6725), vec2(-0.7260,  0.6877), vec2(-0.0544,  0.9985), vec2( 0.8499,  0.5269),
    vec2( 0.9985, -0.0543), vec2( 0.5789, -0.8154), vec2( 0.1903,  0.9817), vec2(-0.7793,  0.6266),
    vec2(-0.8275,  0.5614), vec2( 0.8988,  0.4383), vec2(-0.8399,  0.5428), vec2( 0.6912,  0.7226),
    vec2( 0.1002, -0.9950), vec2(-0.9953, -0.0967), vec2(-0.9794,  0.2017), vec2( 0.4657,  0.8850),

    vec2(-1.0000, -0.0086), vec2( 0.7972, -0.6037), vec2(-0.6686,  0.7436), vec2( 0.5731, -0.8195),
    vec2(-0.9396, -0.3422), vec2(-0.1707,  0.9853), vec2( 0.6155,  0.7881), vec2( 0.2622,  0.9650),
    vec2(-0.9558,  0.2941), vec2(-0.5893, -0.8079), vec2(-0.7713,  0.6365), vec2(-0.0929, -0.9957),
    vec2( 0.7800,  0.6257), vec2( 0.3788,  0.9255), vec2( 0.3608,  0.9327), vec2(-0.9798, -0.1998),

    vec2(-0.6766,  0.7363), vec2(-0.7269, -0.6867), vec2( 0.8173,  0.5762), vec2( 0.7854,  0.6190),
    vec2(-0.3049,  0.9524), vec2( 0.9775, -0.2108), vec2(-0.9189,  0.3946), vec2( 0.9678, -0.2517),
    vec2(-0.4212, -0.9070), vec2( 0.0591, -0.9983), vec2( 0.8952, -0.4457), vec2( 0.7884, -0.6152),
    vec2( 0.1470, -0.9891), vec2( 0.9897, -0.1432), vec2( 0.7101, -0.7041), vec2(-0.9995, -0.0305),

    vec2( 0.9960, -0.0892), vec2(-0.6044,  0.7967), vec2(-0.7756, -0.6312), vec2(-0.5238,  0.8518),
    vec2(-0.9409,  0.3386), vec2(-0.2099, -0.9777), vec2( 0.4556, -0.8902), vec2( 0.9360,  0.3519),
    vec2( 0.9946,  0.1040), vec2( 0.7849, -0.6196), vec2(-0.9269, -0.3753), vec2(-0.6764, -0.7365),
    vec2(-0.4194,  0.9078), vec2( 0.3925, -0.9197), vec2( 0.9815, -0.1914), vec2(-0.9903, -0.1391),

    vec2(-0.9862,  0.1655), vec2( 0.2747,  0.9615), vec2(-0.8160, -0.5780), vec2( 0.7242, -0.6896),
    vec2(-0.2738,  0.9618), vec2( 0.8238,  0.5669), vec2( 0.6327, -0.7744), vec2(-0.0670,  0.9978),
    vec2(-0.6059, -0.7956), vec2( 0.2961, -0.9551), vec2(-0.1238,  0.9923), vec2( 0.1987,  0.9801),
    vec2( 0.4535,  0.8912), vec2( 0.8356,  0.5494), vec2( 0.5857, -0.8105), vec2( 0.0203, -0.9998),

    vec2(-0.6717,  0.7408), vec2(-0.9642, -0.2651), vec2(-0.3585, -0.9335), vec2(-0.5892,  0.8080),
    vec2(-0.8539,  0.5204), vec2(-0.7415, -0.6709), vec2( 0.7048, -0.7094), vec2( 0.5215,  0.8532),
    vec2( 0.2527, -0.9675), vec2(-0.9671, -0.2543), vec2(-0.7412, -0.6713), vec2(-0.2250, -0.9744),
    vec2( 0.6290, -0.7774), vec2( 0.7999, -0.6002), vec2(-0.7680, -0.6404), vec2(-0.3523, -0.9359),

    vec2(-0.1246, -0.9922), vec2(-0.8720, -0.4895), vec2( 0.9282, -0.3721), vec2(-0.7675, -0.6410),
    vec2( 0.5140, -0.8578), vec2( 0.9152, -0.4029), vec2(-0.3747, -0.9271), vec2( 0.0871, -0.9962),
    vec2(-0.6412, -0.7674), vec2(-0.2507, -0.9681), vec2(-0.9922, -0.1247), vec2( 0.2337,  0.9723),
    vec2( 0.7518, -0.6594), vec2(-0.7746,  0.6324), vec2( 0.9269, -0.3754), vec2( 0.9566,  0.2916),

    vec2(-0.8266,  0.5627), vec2(-0.0487,  0.9988), vec2(-0.6318, -0.7751), vec2(-0.7945,  0.6073),
    vec2(-0.5119, -0.8590), vec2(-0.0646,  0.9979), vec2( 0.9601, -0.2796), vec2(-0.8533, -0.5214),
    vec2( 0.5611, -0.8278), vec2(-0.4491,  0.8935), vec2( 0.8496, -0.5274), vec2(-0.4176, -0.9086),
    vec2(-0.7217,  0.6922), vec2(-0.2268, -0.9739), vec2(-0.1818, -0.9833), vec2( 0.9891,  0.1471),

    vec2(-0.9928, -0.1197), vec2(-0.4035, -0.9150), vec2(-0.5750,  0.8181), vec2( 0.1492,  0.9888),
    vec2(-0.9420,  0.3355), vec2(-0.2261, -0.9741), vec2( 0.8712, -0.4909), vec2( 0.3936, -0.9193),
    vec2(-0.7288,  0.6847), vec2(-0.3262, -0.9453), vec2( 0.7088, -0.7054), vec2( 0.0799, -0.9968),
    vec2( 0.6698,  0.7425), vec2(-0.1566,  0.9877), vec2( 0.5027, -0.8644), vec2( 0.3836, -0.9235),
    
    vec2(-0.6461, 0.7633), vec2( 0.1163,-0.9932), vec2( 0.3344, 0.9424), vec2( 0.7127,-0.7014),
    vec2(-0.8596,-0.5109), vec2(-0.9344,-0.3563), vec2( 0.7430, 0.6693), vec2( 0.2133, 0.9770),
    vec2( 0.5804, 0.8143), vec2( 0.5128,-0.8585), vec2( 0.8621,-0.5068), vec2(-0.8259,-0.5638),
    vec2( 0.6577,-0.7533), vec2(-0.5417,-0.8406), vec2(-0.9951,-0.0987), vec2(-0.8237, 0.5671),

    vec2(-0.9981, 0.0618), vec2( 0.9966, 0.0827), vec2(-0.9687,-0.2480), vec2( 0.6012,-0.7991),
    vec2(-0.9459,-0.3244), vec2(-0.1248,-0.9922), vec2(-0.2126, 0.9771), vec2(-0.8804,-0.4742),
    vec2( 0.1235, 0.9923), vec2( 0.5581,-0.8298), vec2(-0.9131,-0.4078), vec2(-0.3142,-0.9494),
    vec2(-0.6767,-0.7362), vec2(-0.9455, 0.3257), vec2(-0.9690,-0.2472), vec2( 0.5434,-0.8395),

    vec2( 0.7401,-0.6725), vec2(-0.7260, 0.6877), vec2(-0.0544, 0.9985), vec2( 0.8499, 0.5269),
    vec2( 0.9985,-0.0543), vec2( 0.5789,-0.8154), vec2( 0.1903, 0.9817), vec2(-0.7793, 0.6266),
    vec2(-0.8275, 0.5614), vec2( 0.8988, 0.4383), vec2(-0.8399, 0.5428), vec2( 0.6912, 0.7226),
    vec2( 0.1002,-0.9950), vec2(-0.9953,-0.0967), vec2(-0.9794, 0.2017), vec2( 0.4657, 0.8850),

    vec2(-1.0000,-0.0086), vec2( 0.7972,-0.6037), vec2(-0.6686, 0.7436), vec2( 0.5731,-0.8195),
    vec2(-0.9396,-0.3422), vec2(-0.1707, 0.9853), vec2( 0.6155, 0.7881), vec2( 0.2622, 0.9650),
    vec2(-0.9558, 0.2941), vec2(-0.5893,-0.8079), vec2(-0.7713, 0.6365), vec2(-0.0929,-0.9957),
    vec2( 0.7800, 0.6257), vec2( 0.3788, 0.9255), vec2( 0.3608, 0.9327), vec2(-0.9798,-0.1998),
    
    vec2(-0.7374,  0.6755), vec2( 0.2325, -0.9726), vec2( 0.8870,  0.4617), vec2(-0.9931, -0.1174),
    vec2( 0.5299,  0.8480), vec2(-0.3546,  0.9350), vec2( 0.9990,  0.0452), vec2(-0.8035, -0.5953),
    vec2( 0.1179,  0.9930), vec2(-0.6747, -0.7381), vec2( 0.9511, -0.3090), vec2(-0.0835, -0.9965),
    vec2( 0.7071,  0.7071), vec2(-0.9135,  0.4067), vec2( 0.4067, -0.9135), vec2(-0.2249,  0.9744)
);

const vec2 NOISE4[64] = vec2[64](
    vec2(-0.6461,  0.7633), vec2( 0.1163, -0.9932), vec2( 0.3344,  0.9424), vec2( 0.7127, -0.7014),
    vec2(-0.8596, -0.5109), vec2(-0.9344, -0.3563), vec2( 0.7430,  0.6693), vec2( 0.2133,  0.9770),
    vec2( 0.5804,  0.8143), vec2( 0.5128, -0.8585), vec2( 0.8621, -0.5068), vec2(-0.8259, -0.5638),
    vec2( 0.6577, -0.7533), vec2(-0.5417, -0.8406), vec2(-0.9951, -0.0987), vec2(-0.8237,  0.5671),

    vec2(-0.9981,  0.0618), vec2( 0.9966,  0.0827), vec2(-0.9687, -0.2480), vec2( 0.6012, -0.7991),
    vec2(-0.9459, -0.3244), vec2(-0.1248, -0.9922), vec2(-0.2126,  0.9771), vec2(-0.8804, -0.4742),
    vec2( 0.1235,  0.9923), vec2( 0.5581, -0.8298), vec2(-0.9131, -0.4078), vec2(-0.3142, -0.9494),
    vec2(-0.6767, -0.7362), vec2(-0.9455,  0.3257), vec2(-0.9690, -0.2472), vec2( 0.5434, -0.8395),

    vec2( 0.7401, -0.6725), vec2(-0.7260,  0.6877), vec2(-0.0544,  0.9985), vec2( 0.8499,  0.5269),
    vec2( 0.9985, -0.0543), vec2( 0.5789, -0.8154), vec2( 0.1903,  0.9817), vec2(-0.7793,  0.6266),
    vec2(-0.8275,  0.5614), vec2( 0.8988,  0.4383), vec2(-0.8399,  0.5428), vec2( 0.6912,  0.7226),
    vec2( 0.1002, -0.9950), vec2(-0.9953, -0.0967), vec2(-0.9794,  0.2017), vec2( 0.4657,  0.8850),

    vec2(-1.0000, -0.0086), vec2( 0.7972, -0.6037), vec2(-0.6686,  0.7436), vec2( 0.5731, -0.8195),
    vec2(-0.9396, -0.3422), vec2(-0.1707,  0.9853), vec2( 0.6155,  0.7881), vec2( 0.2622,  0.9650),
    vec2(-0.9558,  0.2941), vec2(-0.5893, -0.8079), vec2(-0.7713,  0.6365), vec2(-0.0929, -0.9957),
    vec2( 0.7800,  0.6257), vec2( 0.3788,  0.9255), vec2( 0.3608,  0.9327), vec2(-0.9798, -0.1998)
);

const float Bayer5[25] = float[25](
    0.00, 0.48, 0.12, 0.60, 0.24,
    0.32, 0.80, 0.44, 0.92, 0.56,
    0.08, 0.40, 0.04, 0.52, 0.16,
    0.64, 0.96, 0.76, 0.28, 0.88,
    0.72, 0.20, 0.84, 0.36, 0.68
);

vec3 ReconstructVSPos(vec2 uv, float depth)
{
    vec4 ndc = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 view = uInvProj * ndc;
    return view.xyz / view.w;
}

vec2 GetConstNoise()
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    int index = (pixel.x % 4) + (pixel.y % 4) * 4;
    return NOISE[index].xy;
}

vec2 GetConstNoise2()
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    int index = (pixel.x % 4) + (pixel.y % 4) * 4;
    return samples[index].xy;
}

vec2 GetRandomNoise()
{
    return fract(sin(vec2(dot(TexCoords, vec2(12.9898, 78.233)), dot(TexCoords, vec2(39.3467, 11.1354)))) * 43758.5453);
}

vec2 GetConstNoise3()
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    ivec2 p = pixel % 4;
    int idx = p.x + p.y * 4;
    return NOISE2[idx];
}

vec2 GetConstNoise4()
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    ivec2 p = pixel % 16;
    int idx = p.x + p.y * 16;
    return NOISE3[idx];
}

vec2 GetConstNoise5()
{
    ivec2 pixel = ivec2(gl_FragCoord.xy);
    ivec2 p = pixel % 8;
    int idx = p.x + p.y * 8;
    return NOISE4[idx];
}

float Bayer(uvec2 p, uint level)
{
    p = (p ^ (p << 8u)) & uvec2(0x00ff00ffu);
    p = (p ^ (p << 4u)) & uvec2(0x0f0f0f0fu);
    p = (p ^ (p << 2u)) & uvec2(0x33333333u);
    p = (p ^ (p << 1u)) & uvec2(0x55555555u);

    uint i = (p.x ^ p.y) | (p.x << 1u);

    i = bitfieldReverse(i);
    i >>= (32u - level * 2u);

    return float(i) / float(1u << (2u * level));
}

vec2 GetBayerNoise(vec2 xy, vec2 vpos)
{
    ivec2 pp = ivec2(int(vpos.x) % 5, int(vpos.y) % 5);
    float dir = Bayer5[pp.x + 5 * pp.y];
    float second = Bayer(uvec2(uint(vpos.x), uint(vpos.y)), 3u);
    return vec2(dir, second);
}

float SAO(vec2 xy, vec3 verPos, vec3 n, vec2 noise, float radius)
{
    float g  = 1.32471795724474602596; // plastic constant
    vec2 ng  = 1.0 / vec2(g, g * g);
    float vl = length(verPos);

    vec2 acc = vec2(0.0);

    for (int i = 0; i <= SLICES * STEPS; i++)
    {
        vec2 ns = vec2(
            6.2831853 * ((noise.x + float(i)) / float(SLICES * STEPS)),
            fract(noise.y + float(i) / g) * radius / vl
        );

        vec2 nxy = xy + ns.y * vec2(sin(ns.x), cos(ns.x)) * vec2(1.0, uScreenSize.x / uScreenSize.y);

        // original ReShade odd guard (optional, can remove)
        vec2 rg = clamp(nxy * nxy - nxy, 0.0, 1.0);
        if (rg.x != -rg.y)
        {
            continue;
        }
        
        // reconstruct sample position in view space
        float sampleDepth = texture(uDepthTex, nxy).r;
        if (sampleDepth >= 1.0)
        {
            continue; // skip skybox / background
        }
        
        vec3 samPos = ReconstructVSPos(nxy, sampleDepth);
        vec3 tv     = samPos - verPos;

        acc += vec2(max(0.0, dot(tv, n)) / (dot(tv, tv) + 0.1), 1.0);
    }

    return pow(clamp(1.0 - SIGMA * 2.0 * acc.x / acc.y, 0.0, 1.0), SAO_K);
}

vec4 fragment()
{
    float depth = texture(uDepthTex, TexCoords).r;
    if (depth >= 1.0) {
        return vec4(1);
    }

    vec3 posVS = ReconstructVSPos(TexCoords, depth);

    vec2 texelSize = 1.0 / uScreenSize;
    float depthRight = texture(uDepthTex, TexCoords + vec2(texelSize.x, 0)).r;
    float depthDown  = texture(uDepthTex, TexCoords + vec2(0, texelSize.y)).r;
    vec3 posX = ReconstructVSPos(TexCoords + vec2(texelSize.x, 0), depthRight);
    vec3 posY = ReconstructVSPos(TexCoords + vec2(0, texelSize.y), depthDown);
    vec3 n = normalize(cross(posX - posVS, posY - posVS));

    vec2 noise = vec2(0);
    
    if (aoMethod == 0) {
        noise = GetConstNoise();
    } else if (aoMethod == 1) {
        noise = GetConstNoise2();
    } else if (aoMethod == 2) {
        noise = GetRandomNoise();
    } else if (aoMethod == 3) {
        noise = GetBayerNoise(TexCoords, posVS.xy);
    } else if (aoMethod == 4) {
        noise = GetConstNoise3();
    } else if (aoMethod == 5) {
        noise = GetConstNoise4();
    } else if (aoMethod == 6) {
        noise = GetConstNoise5();
    }
    
    float ao = SAO(TexCoords, posVS, n, noise, uRadius);
    return vec4(ao, ao, ao, 1.0);
}
#endif

#ifdef VERTEX
vec4 vertex()
{
    TexCoords = aTexCoords;
    return vec4(aPos, 1.0);
}
#endif