# Lit_Fade

Shader: `Universal Render Pipeline/Lit_Fade`

Asset: `Assets/Game/Shaders/Lit_Fade.shader`

Select this shader on a material, retain its existing Lit settings, and use the
**Fade** slider below the normal Lit inspector. `_Fade` defaults to 1 and accepts
the existing renderer MaterialPropertyBlock control without script changes.

- Transparent: final alpha is stock Lit alpha multiplied by `_Fade`. Premultiplied
  RGB also fades, including specular and emission; Multiply blends toward neutral
  white. Stock Alpha, Premultiply, Additive and Multiply blend settings remain.
- Opaque: intermediate visibility uses stable pixel coverage dithering, retaining
  the stock opaque render queue, depth writes and deferred support. This is not
  transparent glass blending. Depth, depth normals, shadows and motion vectors
  use the same coverage rule. At 1 no coverage is removed; at 0 all is removed.
- Baked lighting uses the unchanged stock Meta pass. Runtime occlusion fading
  does not rewrite baked lightmaps or baked shadows.

The shader pass declarations and material input include derive from **URP 17.5.0**
under the included Unity Companion License notice. Fragment adapters call the
installed stock Lit pass implementations; they do not replace the lighting model.
The Editor inspector delegates to Unity's stock Lit inspector and adds one slider.
When upgrading URP, compare `Lit.shader` and `LitInput.hlsl` with these copies and
rerun the review; the inspector currently resolves URP's internal `LitShader` type.

## Validation — Unity 6000.5.6f1, D3D11 Editor Play Mode

`Lit_FadeReview.Run` creates an unsaved empty scene and temporary material copies.
Run from a disposable/batch Editor session (it exits the Editor on completion).
It does not save production scenes, materials or rendering-profile assets.

- Existing `M_Weapon_Glass`: reproduced stock Lit ignoring MPB `_Fade = 0`.
- Temporary replacement: compared stock Lit with Fade 1, 0.5 and 0 through MPB.
- Mobile and PC profiles: full-visibility pixel comparison passed; zero matched
  background; transparent half-fade matched a 50% blend toward background.
- Exercised all four transparent blend modes plus opaque and alpha-cutout surfaces;
  included metallic/emissive variants and compiled retained auxiliary passes.
- Visually inspected the glass comparison strips. No shader compiler errors in
  the final run. No device testing, production material reassignment, or changes
  to existing shaders/occlusion code.

Local review evidence: `Logs/LitFade/Unity.log`, `result.txt`,
`Mobile-stock-full-half-zero.png`, `PC-stock-full-half-zero.png`.

## Android investigation and assignment fix — 22 September 2026

The installed failing APK matched `Builds/Kids_VS_Aliens_V_0.0.24.apk`
(SHA256 `D6750D9A19BA7D3929ED2BF53073BE8D97E07BFBE07EE4FE3D38AB82A1E8B885`).
Its serialized `M_Weapon_Glass` referenced **KVA/Beam Lit Fade**, which exposes
`_BeamVisibility`, not `_Fade`. The APK did not contain Lit_Fade. The saved material
had the same assignment; the excavator FBX's `12 - Default` material remap resolves
to it, on renderer `12_-_Default` in `PF_Excavator_A`.

The earlier shader review used a temporary Lit_Fade material copy. That demonstrated
the shader, but did not ensure the production material/build referenced it.

Reproduced on the real excavator glass in an isolated scene, in both Editor and a
separate diagnostic Android app. The latter used the production Mobile renderer,
IL2CPP/ARM64, Vulkan, and the connected OnePlus CPH2493 (Mali-G710 MC10).
`CameraOcclusionAuthoring.ApplyFade` wrote the actual renderer's MPB, read back as
1 / 0.5 / 0. No material replacement occurred in this fixture.

| Android glass configuration | Image difference from empty background at 1 / 0.5 / 0 |
|---|---|
| Original saved Beam shader | 0.02656054 / 0.02656054 / 0.02656054 |
| Corrected saved Lit_Fade material | 0.02656054 / 0.01416709 / 0 |

Values are mean absolute RGB differences from GPU render readback. Screenshots
were also visually inspected. The fixed material's half-fade passed comparison
with the expected blend; zero matched the empty background exactly.

Both configurations retained queue 3000, Surface 1, SrcBlend One, DstBlend
OneMinusSrcAlpha, ZWrite 0, `_SURFACE_TYPE_TRANSPARENT` and
`_ALPHAPREMULTIPLY_ON`. Lit_Fade was supported and its compiled Android `_Fade`
actively controlled pixels. This was a saved material/build-content mismatch,
not a demonstrated platform shader/compiler or URP renderer bug. No stripping
settings or Always Included Shaders workaround was needed.

The production fix changes only the glass material's shader reference to Lit_Fade
and stores `_Fade = 1`. Its existing shared references stay intact. No occlusion,
Beam shader, pipeline, prefab, scene or gameplay code changed. Existing controller
code consequently takes its `_Fade` path instead of the legacy transparency
fallback. The new `LitFadeMaterialTests` asset regression checks the **saved real
prefab/material** to catch this mismatch before another build. The stock comparison
review now explicitly creates its stock-Lit baseline even when the source material
already uses Lit_Fade.

Evidence: `Logs/LitFadeAndroid/EditorBaseline`, `Editor`, `Android`, `AndroidFixed`,
`Diagnosis.txt`, and `EditorFixed.log`. Temporary fixture source was archived under
`Logs/LitFadeAndroid/FixtureSource` and removed from Assets after validation.

The normal game was rebuilt successfully at
`Builds/RunInterface/KidsVsAliens-Development.apk` (log:
`Logs/LitFadeAndroid/ProductionBuild.log`). Reading the resulting APK confirmed
its serialized `M_Weapon_Glass` points to `Universal Render Pipeline/Lit_Fade`,
stores `_Fade = 1`, and retains both transparent/premultiplied keywords.
Installed that game APK successfully with `adb install -r` (no game-data clear),
launched the game, and uninstalled the separate `com.kva.litfadeprobe` diagnostic
app. Device package update time: 22 September 2026, 14:06:33.
