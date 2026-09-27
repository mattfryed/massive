#ifndef MASSIVE_PLAYER_SURFACE_INCLUDED
#define MASSIVE_PLAYER_SURFACE_INCLUDED
// Player previews run a closed 20-second orbit. Their field has no joystick
// input, lettering, or tessellation; settled and forming states share it.
static const float4 _Attraction=float4(0,0,0,0);
#include "AttractFerrofluidField.hlsl"

float playerDensity()
{
    return lerp(min(2.5,_Density),_Density,smoothstep(.24,.8,_FormationCore));
}
float formingSurface(float3 n)
{
    float3 unused;
    return fluidSurfaceAtDensity(n,playerDensity(),unused);
}
float formingSurfaceGradient(float3 n,out float3 derivative)
{
    float3 unused;
    return fluidSurfaceGradient(n,playerDensity(),unused,derivative);
}
float playerFixture(float3 ray,float3 center,float width,float height)
{
    center=normalize(center);float3 u=normalize(cross(float3(0,1,0),center)),v=cross(center,u);
    float2 p=float2(dot(ray,u)/width,dot(ray,v)/height),p2=p*p;
    return step(dot(p2*p2,float2(1,1)),1)*step(.3,dot(ray,center));
}
float playerWhite(float3 normal,float3 radial,float3 viewPosition,float moundHeight)
{
    if(_WhiteDominant>.5)return step(.425-saturate(_Wetness)*.06,moundHeight);
    float3 n=normalize(normal),v=normalize(lerp(-viewPosition,float3(0,0,1),unity_OrthoParams.w));
    float3 reflected=reflect(-v,n);
    float scale=lerp(.55,1.2,saturate(_Wetness));
    float white=max(playerFixture(reflected,float3(-.55,.55,.7),.20*scale,.62*scale),
        max(playerFixture(reflected,float3(.72,-.15,.45),.11*scale,.48*scale),playerFixture(reflected,float3(.1,.78,-.4),.62*scale,.13*scale)));
    radial=normalize(radial);
    float angle=radians(_RimAngle);float3 backLight=normalize(float3(cos(angle),sin(angle),-1.7));
    float rim=step(.0001,_RimWidth)*step(dot(radial,v),_RimWidth)*step(.12,dot(radial.xy,backLight.xy))*step(.16,dot(n,backLight));
    return max(white,rim);
}
#endif
