#ifndef MASSIVE_MULTIPLIER_SHIMMER
#define MASSIVE_MULTIPLIER_SHIMMER
float4 _MultiplierShimmer; // strength, reach, clock, pattern scale
float4 _MultiplierShimmerDetail; // contrast, curl
float4 _MultiplierPaletteA, _MultiplierPaletteB, _MultiplierPaletteC;
float MultiplierHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float MultiplierNoise(float2 p)
{
    float2 cell = floor(p), f = frac(p); f = f*f*(3-2*f);
    return lerp(lerp(MultiplierHash(cell), MultiplierHash(cell+float2(1,0)), f.x),
        lerp(MultiplierHash(cell+float2(0,1)), MultiplierHash(cell+1), f.x), f.y);
}
// Curled perimeter filaments and slow palette drift echo the Amplifier goal corona.
float4 MultiplierShimmer(float angle, float outward01)
{
    float time = _MultiplierShimmer.z, scale = max(.2, _MultiplierShimmer.w);
    float2 perimeter = float2(cos(angle), sin(angle));
    float activity = .65 + .18*sin(angle*3 + time*.6) + .17*sin(angle*7 - time*.4);
    float bend = (angle*48 + outward01*7)/scale +
        sin(angle*16/scale - outward01*3 + time*.32)*2.5*_MultiplierShimmerDetail.y;
    float aa = max(.06, min(.65, fwidth(bend)));
    float threads = 1-smoothstep(.06, .24+aa, abs(sin(bend)));
    float noise = MultiplierNoise(perimeter*8/scale + float2(outward01*2, -time*.17));
    float pattern = max(0, .6 + (threads*.65 + noise*.35 - .5)*_MultiplierShimmerDetail.x);
    float outer = exp(-pow(max(0, outward01)/max(.2, activity*.65), 1.6));
    outer *= 1-smoothstep(.75, 1, max(0, outward01));
    float palette = frac(angle/6.2831853 + time*.045 + outward01*.09)*3;
    float3 rgb = palette<1 ? lerp(_MultiplierPaletteA.rgb, _MultiplierPaletteB.rgb, palette) :
        palette<2 ? lerp(_MultiplierPaletteB.rgb, _MultiplierPaletteC.rgb, palette-1) :
                    lerp(_MultiplierPaletteC.rgb, _MultiplierPaletteA.rgb, palette-2);
    return float4(rgb, saturate(outer * pattern * _MultiplierShimmer.x));
}
#endif
