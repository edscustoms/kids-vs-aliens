#ifndef BIKE_ROUTE_SURFACE_SAMPLING
#define BIKE_ROUTE_SURFACE_SAMPLING
float2 SurfaceHash(float2 p)
{
    float3 h=frac(p.xyx*float3(.1031,.1030,.0973));
    h+=dot(h,h.yzx+33.33);
    return frac((h.xx+h.yz)*h.zy);
}
float SurfaceNoise(float2 p)
{
    float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(SurfaceHash(cell).x,SurfaceHash(cell+float2(1,0)).x,f.x),
        lerp(SurfaceHash(cell+float2(0,1)).x,SurfaceHash(cell+1).x,f.x),f.y);
}
struct RoadSample { half3 color; half2 bump; half ao; half smoothness; };
RoadSample SampleRoadPatch(TEXTURE2D_PARAM(a,s),TEXTURE2D_PARAM(n,ns),TEXTURE2D_PARAM(m,ms),
    float2 uv,float2 dx,float2 dy,float2 seed)
{
    float2 r=normalize(float2(1,(seed.y-.5)*1.2));
    float2x2 rotation=float2x2(r.x,-r.y,r.y,r.x);
    float frequency=lerp(.8,1.2,seed.x);
    uv=mul(rotation,uv)*frequency+seed*9;
    dx=mul(rotation,dx)*frequency;dy=mul(rotation,dy)*frequency;
    RoadSample result;
    result.color=SAMPLE_TEXTURE2D_GRAD(a,s,uv,dx,dy).rgb;
    result.bump=mul(UnpackNormal(SAMPLE_TEXTURE2D_GRAD(n,ns,uv,dx,dy)).xy,rotation);
    half4 mask=SAMPLE_TEXTURE2D_GRAD(m,ms,uv,dx,dy);
    result.ao=mask.g;result.smoothness=mask.a;
    return result;
}
RoadSample SampleRoadSurface(TEXTURE2D_PARAM(a,s),TEXTURE2D_PARAM(n,ns),TEXTURE2D_PARAM(m,ms),float2 uv)
{
    float2 dx=ddx(uv),dy=ddy(uv);
    float selection=SurfaceNoise(uv*.39)*9;
    float cell=floor(selection);half blend=smoothstep(.15,.85,frac(selection));
    RoadSample first=SampleRoadPatch(TEXTURE2D_ARGS(a,s),TEXTURE2D_ARGS(n,ns),TEXTURE2D_ARGS(m,ms),uv,dx,dy,SurfaceHash(float2(cell,8.7)));
    RoadSample second=SampleRoadPatch(TEXTURE2D_ARGS(a,s),TEXTURE2D_ARGS(n,ns),TEXTURE2D_ARGS(m,ms),uv,dx,dy,SurfaceHash(float2(cell+1,8.7)));
    RoadSample result;
    result.color=lerp(first.color,second.color,blend);result.bump=lerp(first.bump,second.bump,blend);
    result.ao=lerp(first.ao,second.ao,blend);result.smoothness=lerp(first.smoothness,second.smoothness,blend);
    return result;
}
#endif
