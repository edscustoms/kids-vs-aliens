// Preserve URP motion vector calculation, with the same opaque fade coverage.
#include "Lit_FadeCoverage.hlsl"
#define frag StockMotionFragment
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
#undef frag
float4 frag(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    LitFadeCoverage(input.positionCS.xy);
    return StockMotionFragment(input);
}
