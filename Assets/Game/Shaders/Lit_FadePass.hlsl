// Thin fragment adapters around the installed URP Lit passes. Lighting stays in URP.
#include "Lit_FadeCoverage.hlsl"

#if defined(LIT_FADE_FORWARD)
#define LitPassFragment StockLitPassFragment
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
#undef LitPassFragment
void LitPassFragment(Varyings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    LitFadeColorCoverage(input.positionCS.xy);
    StockLitPassFragment(input, outColor
#ifdef _WRITE_RENDERING_LAYERS
        , outRenderingLayers
#endif
    );
    outColor = LitFadeColor(outColor);
}
#elif defined(LIT_FADE_GBUFFER)
#define LitGBufferPassFragment StockLitGBufferPassFragment
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitGBufferPass.hlsl"
#undef LitGBufferPassFragment
GBufferFragOutput LitGBufferPassFragment(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    LitFadeCoverage(input.positionCS.xy);
    return StockLitGBufferPassFragment(input);
}
#elif defined(LIT_FADE_SHADOW)
#define ShadowPassFragment StockShadowPassFragment
#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
#undef ShadowPassFragment
half4 ShadowPassFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    LitFadeCoverage(input.positionCS.xy);
    return StockShadowPassFragment(input);
}
#elif defined(LIT_FADE_DEPTH)
#define DepthOnlyFragment StockDepthOnlyFragment
#include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
#undef DepthOnlyFragment
half DepthOnlyFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    LitFadeCoverage(input.positionCS.xy);
    return StockDepthOnlyFragment(input);
}
#elif defined(LIT_FADE_NORMALS)
#define DepthNormalsFragment StockDepthNormalsFragment
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
#undef DepthNormalsFragment
void DepthNormalsFragment(Varyings input, out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    LitFadeCoverage(input.positionCS.xy);
    StockDepthNormalsFragment(input, outNormalWS
#ifdef _WRITE_RENDERING_LAYERS
        , outRenderingLayers
#endif
    );
}
#elif defined(LIT_FADE_2D)
#define frag Stock2DFragment
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"
#undef frag
half4 frag(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    LitFadeColorCoverage(input.vertex.xy);
    return LitFadeColor(Stock2DFragment(input));
}
#endif
