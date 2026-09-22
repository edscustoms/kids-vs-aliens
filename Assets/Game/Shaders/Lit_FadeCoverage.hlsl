#ifndef LIT_FADE_COVERAGE_INCLUDED
#define LIT_FADE_COVERAGE_INCLUDED

// Opaque surfaces cannot alpha blend without changing stock Lit's render state.
// Stable pixel coverage also keeps depth, normals, motion vectors and shadows in sync.
void LitFadeCoverage(float2 pixelPosition)
{
    float fade = saturate(_Fade);
    if (fade >= 1.0) return;
    clip(fade > 0.0 ? 1.0 : -1.0);
    float threshold = frac(52.9829189 * frac(dot(floor(pixelPosition), float2(0.06711056, 0.00583715))));
    clip(fade - max(threshold, 0.00001));
}

void LitFadeColorCoverage(float2 pixelPosition)
{
    if (IsSurfaceTypeOpaque()) LitFadeCoverage(pixelPosition);
    else clip(_Fade > 0.0 ? 1.0 : -1.0);
}

half4 LitFadeColor(half4 color)
{
    if (IsSurfaceTypeOpaque() || _Fade >= 1.0) return color;
    half fade = saturate(_Fade);
    color.a *= fade;
    // UnityEngine.Rendering.BlendMode: One=1, DstColor=2, Zero=0.
    // Alpha blending already scales RGB by the faded alpha. Premultiplied
    // output must also fade RGB (including preserved specular, emission and fog).
    if (_SrcBlend == 1.0) color.rgb *= fade;
    // Multiply's neutral contribution is white, not black.
    else if (_SrcBlend == 2.0 && _DstBlend == 0.0) color.rgb = lerp(half3(1,1,1), color.rgb, fade);
    return color;
}
#endif
