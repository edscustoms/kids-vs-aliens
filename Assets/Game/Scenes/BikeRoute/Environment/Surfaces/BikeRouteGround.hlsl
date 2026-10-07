// Ground-only sampling. The Terrain's accepted cliff path never enters ApplyGround.
TEXTURE2D_ARRAY(_GroundColors); SAMPLER(sampler_GroundColors);
TEXTURE2D_ARRAY(_GroundRelief);
TEXTURE2D(_GroundRoadDistance); SAMPLER(sampler_GroundRoadDistance);
float4 _GroundField;
half _GroundStrength, _GroundNormalStrength;

struct GroundSample { half3 color; half2 bump; half ao; half smoothness; };
GroundSample GroundPatch(float2 uv,float2 dx,float2 dy,float layer,float2 seed)
{
    float2 turn=normalize(float2(1,(seed.y-.5)*1.6));
    float2x2 rotation=float2x2(turn.x,-turn.y,turn.y,turn.x);
    float scale=lerp(.78,1.23,seed.x);
    uv=mul(rotation,uv)*scale+seed*13;
    dx=mul(rotation,dx)*scale;dy=mul(rotation,dy)*scale;
    half4 c=SAMPLE_TEXTURE2D_ARRAY_GRAD(_GroundColors,sampler_GroundColors,uv,layer,dx,dy);
    half4 r=SAMPLE_TEXTURE2D_ARRAY_GRAD(_GroundRelief,sampler_GroundColors,uv,layer,dx,dy);
    GroundSample result;
    result.color=c.rgb;result.ao=c.a;result.smoothness=r.b;
    result.bump=mul(r.rg*2-1,rotation);
    return result;
}
GroundSample MixGround(GroundSample a,GroundSample b,half weight)
{
    GroundSample r;
    r.color=lerp(a.color,b.color,weight);r.bump=lerp(a.bump,b.bump,weight);
    r.ao=lerp(a.ao,b.ao,weight);r.smoothness=lerp(a.smoothness,b.smoothness,weight);
    return r;
}
GroundSample GroundFamily(float2 p,float2 dx,float2 dy,float layer,float metres)
{
    float selection=RockNoise(p/metres*.36)*11;
    float cell=floor(selection),fraction=frac(selection);
    GroundSample a=GroundPatch(p/metres,dx/metres,dy/metres,layer,RockHash(float2(cell,layer+19)));
    GroundSample b=GroundPatch(p/metres,dx/metres,dy/metres,layer,RockHash(float2(cell+1,layer+19)));
    // Continuous pair changes, with mild cavity contrast to keep aggregate legible.
    half contrast=(a.ao-b.ao)*.4;
    return MixGround(a,b,smoothstep(.15,.85,fraction+contrast*fraction*(1-fraction)));
}
void ApplyGround(float3 p,float3 dx,float3 dy,half3 normal,half soil,half weight,
    inout SurfaceData surface,inout half3 normalWS)
{
    float broad=RockNoise(p.xz*.018);
    float deposits=RockNoise(p.xz*.085+27);
    float distance=SAMPLE_TEXTURE2D_LOD(_GroundRoadDistance,sampler_GroundRoadDistance,
        (p.xz-_GroundField.xy)/_GroundField.zw,0).r*32;
    float width=lerp(2.8,7.5,RockNoise(p.xz*.037+11));
    float brokenEdge=(RockNoise(p.xz*.67)-.5)*1.2;
    half shoulder=1-smoothstep(.25,width,distance+brokenEdge);
    half forest=smoothstep(.12,.88,soil);
    GroundSample gravel=GroundFamily(p.xz,dx.xz,dy.xz,0,3.4);
    GroundSample earth=GroundFamily(p.xz,dx.xz,dy.xz,1,3.0);
    if(forest>.001)
        earth=MixGround(earth,GroundFamily(p.xz,dx.xz,dy.xz,3,3.8),forest*.85);
    half aggregate=saturate(shoulder*.85+(1-shoulder)*lerp(.16,.53,deposits));
    GroundSample ground=MixGround(earth,gravel,aggregate);
    half stones=smoothstep(.43,.78,deposits)*lerp(.55,.2,forest);
    stones=max(stones,shoulder*.68);
    if(stones>.001)
        ground=MixGround(ground,GroundFamily(p.xz,dx.xz,dy.xz,2,7.2),stones);

    half luminance=dot(ground.color,half3(.2126,.7152,.0722));
    ground.color=lerp(luminance.xxx,ground.color,.65);
    half3 regional=lerp(half3(1,.95,.84),half3(.75,.79,.73),forest);
    // Matching dust at the asphalt foot opens into less compacted gravel outside.
    half edge=1-smoothstep(0,2.2,distance);
    half3 tint=lerp(regional,half3(.66,.59,.47),edge*.7);
    half macro=lerp(.8,1.17,smoothstep(.15,.85,broad));
    half cavity=lerp(.78,1,smoothstep(.25,.9,ground.ao));
    surface.albedo=lerp(surface.albedo,ground.color*tint*macro*cavity,weight);
    surface.occlusion=lerp(surface.occlusion,lerp(.3,1,ground.ao),weight);
    surface.smoothness=lerp(surface.smoothness,ground.smoothness*lerp(.34,.62,broad),weight);
    half3 bump=half3(ground.bump.x,0,ground.bump.y);
    bump-=normal*dot(bump,normal);
    normalWS=normalize(lerp(normalWS,normalize(normal+bump*_GroundNormalStrength),weight));
}
