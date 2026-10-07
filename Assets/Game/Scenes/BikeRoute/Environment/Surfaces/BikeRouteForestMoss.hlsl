// Localized cliff material extension. Region coverage is baked from the existing
// route guide; the mask never owns or changes geometry, Terrain paint or ground.
TEXTURE2D(_MossRegion); SAMPLER(sampler_MossRegion);
TEXTURE2D(_MossAlbedo); TEXTURE2D(_MossNormal); TEXTURE2D(_MossMask);
float4 _MossField;
half _MossCoverage, _MossRelief;

void ApplyForestMoss(float3 p,float3 dx,float3 dy,half3 normal,half3 axes,
    half steep,half rockAO,inout SurfaceData surface,inout half3 normalWS)
{
    float2 fieldUV=(p.xz-_MossField.xy)/_MossField.zw;
    // No moss texture work in other regions, even though Terrain shares a material.
    if(any(fieldUV<=0) || any(fieldUV>=1))return;
    half3 region=SAMPLE_TEXTURE2D_LOD(_MossRegion,sampler_MossRegion,fieldUV,0).rgb;
    if(region.r<=0)return;

    float broad=RockNoise(p.xz*.071+float2(p.y*.035,17));
    float pockets=RockNoise(float2(p.x*.16+p.z*.11,p.y*.19)+31);
    half cavities=saturate((1-rockAO)*2.8);
    // Sheltered concavities, upward-facing ledges and existing rock cavities favor
    // growth, but broad open stone gaps remain between the connected patches.
    half habitat=saturate(.18+region.g*.36+cavities*.38+max(0,normal.y)*.28);
    half patch=smoothstep(.39,.68,broad*.48+pockets*.26+habitat*.26);
    half coverage=region.r*region.b*steep*patch*_MossCoverage;
    if(coverage<=.001)return;

    // Reuse the accepted anti-tiling triplanar sampler, including paired samples,
    // normal-basis rotation and explicit gradients. Enlarged fracture structure reads
    // at bike-camera distance; accepted rock sampling retains the fine grain.
    Layer moss=ProjectLayer(TEXTURE2D_ARGS(_MossAlbedo,sampler_Splat0),
        TEXTURE2D_ARGS(_MossNormal,sampler_Normal0),TEXTURE2D_ARGS(_MossMask,sampler_Mask0),
        p,dx,dy,float4(_TerrainWorldSize.xy/7.5,0,0),axes,1.35,1);
    half colony=coverage;
    half fiber=saturate((moss.albedo.g-moss.albedo.b)*6);
    // Detail boundaries come from the scan's moss/stone contrast, not green noise.
    coverage*=smoothstep(.08,.65,fiber*.58+(1-moss.mask.g)*.24+patch*.3);
    half3 mossColor=moss.albedo*lerp(half3(.80,.86,.69),half3(.95,1.02,.86),broad);
    half cavityShade=lerp(.64,1,smoothstep(.18,.95,moss.mask.g));
    surface.albedo=lerp(surface.albedo,mossColor*cavityShade,coverage);
    // Keep the scan's larger fissures coherent through moss and exposed stone,
    // instead of applying normal/cavity detail only to the green-colored pixels.
    surface.albedo*=lerp(1,lerp(.64,1,smoothstep(.12,.83,moss.mask.g)),colony*.75);
    surface.smoothness=lerp(surface.smoothness,min(.22h,moss.mask.a*.38h),coverage);
    surface.occlusion=lerp(surface.occlusion,min(surface.occlusion,max(.25h,moss.mask.g)),colony*.9);
    // Add tangent relief to the accepted rock normal; never replace it with a soft
    // moss normal. Base rock fissures continue to react to light beneath the patch.
    half3 relief=moss.perturbation-normal*dot(moss.perturbation,normal);
    normalWS=normalize(normalWS+relief*colony*_MossRelief);
}
