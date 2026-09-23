# ConstructionSite lighting — 21 September 2026

ConstructionSite now has its own provisional bake and lighting configuration. This pass changes lighting authoring, lightmap UVs and shadow participation; it does not change gameplay code or level geometry.

## Rebaking

1. Open `Assets/Game/Scenes/ConstructionSite.unity` by itself, outside Play Mode.
2. Make the intended static-geometry or baked-light edits and save the scene.
3. Run **Tools → Lighting → Bake Construction Site Lighting**.
4. Wait for the completion message before closing Unity or continuing Play Mode testing.

The Editor-only command calls Unity's `Lightmapping.BakeAsync()`, uses the scene's assigned `Assets/Game/Settings/Lighting/ConstructionSite.lighting`, and saves the resulting scene lighting references on successful completion. It logs start, completion, cancellation and errors. It rejects an unsaved/wrong scene, missing/wrong settings, Play Mode, or an already-running bake. It never reapplies renderer flags, moves objects, retunes lights/materials or changes graphics/quality settings.

New static geometry still needs normal Unity lighting authoring: Contribute GI, suitable lightmap UVs and appropriate lightmap scale. Moving geometry must stay out of the static bake. These decisions are intentionally **not** made by the convenience command. Fixed future floodlights should normally use baked lights plus visible/emissive fixtures; retain adequate probe coverage for characters.

## Saved configuration

| Area | Configuration |
|---|---|
| Main directional light | Mixed / Shadowmask; existing intensity, color, angle and realtime shadow quality retained |
| Static building, containers, fences, barriers, fixed props and terrain | Baked indirect lighting and static shadowmask; Cast Shadows retained so the bake contains their shadows |
| Fixed cyan / purple accent lights | Baked, preserving their authored color, intensity, range and placement |
| Amy / aliens | Dynamic, realtime character shadows retained, lit by light probes |
| Alien sweep | Realtime illumination ON, shadows OFF; this was already correct and its movement was not changed |
| Both excavators | Entire assemblies remain dynamic; their material-based meshes mix fixed and potentially moving parts, so no permanent arm/bucket shadow is baked |
| GrassDry A / B / C | Local prefab variants with shadow casting OFF; original source prefabs, grass placement, density, dimensions and colors retained |
| BushDry A / B | Original medium-bush shadow participation retained |
| Dropped pistol / rifle / electric grenade | Mesh shadow casting OFF in their dropped prefabs; held/equipped/thrown variants and all item behavior unchanged |
| Camera fill, Beam and preview lighting | Unchanged |

952 mesh renderers and nine terrains have lightmap assignments. The scene retains its 225 authored light probes and adds 694 samples across accessible ground/floors. The three existing 128-resolution reflection probes were rebaked without changing their volumes or settings.

The provisional profile uses Progressive GPU, 12 texels/m, a 2048 atlas, four-pixel padding, 32 direct / 64 indirect / 64 environment samples, two bounces, directional lightmaps, baked environment lighting, indirect output scale 2 and AO disabled. Current output is one atlas set: color, direction and shadowmask. These are intentionally review-quality settings, not a final production bake for a locked level.

The existing **Mobile** quality uses Shadowmask, so static objects stop contributing to the main realtime shadow map. Existing **PC** quality retains Distance Shadowmask and can still submit static casters nearby. Neither URP asset, renderer feature list nor project-wide quality/graphics settings was changed. Mobile still uses one cascade, 1024 main-shadow resolution, 50 m shadow distance and additional-light shadows disabled. See Unity's [URP shadow optimization guidance](https://docs.unity3d.com/6000.0/Documentation/Manual/shadows-optimization.html).

Before this pass, ConstructionSite referenced StarterAssets Playground lighting assets but had no lightmaps. Most environment renderers lacked Contribute GI/lightmap UVs. Fourteen model import settings now generate secondary UVs, including container Sidewall, whose existing UV2 channel had zero-area triangles. Embedded meshes and the generated cement-bag mesh received UV2 without changing their visible geometry. The cement-bag shader received baked shadowmask sampling/variants and a Meta pass for baking.

## Matched Unity measurements

Normal Unity Editor Play Mode, active Mobile profile, DX11 / GTX 1070 Ti, 951×476 Game View, render scale 0.8. Each value is a rounded 24-frame average. The same four player/camera positions were used before and after. These are submitted rendering counts, **not measured phone GPU timings**. Live alien/particle activity causes small variations.

| Gameplay view | Triangles before → after | Vertices before → after | Draw calls before → after | SetPass before → after |
|---|---:|---:|---:|---:|
| Front balcony | 1,540,560 → 1,029,099 | 1,469,364 → 922,203 | 936 → 503 | 79 → 72 |
| Container approach | 952,908 → 540,201 | 953,396 → 489,395 | 1,003 → 507 | 66 → 60 |
| Excavator | 946,449 → 577,820 | 990,569 → 554,835 | 898 → 424 | 63 → 58 |
| Substation | 944,956 → 545,261 | 1,041,290 → 537,644 | 902 → 285 | 57 → 51 |

This is approximately 33–43% fewer submitted triangles and 46–68% fewer draw calls. Shadow-caster submissions fell from 498/567/546/663 to 67/72/72/46. Temporarily disabling the remaining realtime shadows removed approximately 358k/293k/261k/243k triangles respectively. Important dynamic casters and the excavators still cost work; visible foliage and detailed temporary models also retain their main-camera geometry cost. No meshes were simplified to achieve these results.

The fresh baseline differs slightly from the older diagnosis because it uses the current user-authored scene and current session; it is the appropriate before/after comparison for this pass.

## Validation and visual differences

- Successful Unity compile after removal of the disposable review tools. Real bakes exercised the production command and its completion path; the final bake has no invalid-mesh bake warning.
- Gameplay-camera captures reviewed for balcony, containers, excavator and substation, plus arrival, moving Amy, aliens, firing, Hoist materialization/travel/landing and occlusion.
- Live ConstructionSite Play Mode smoke passed normal movement, camera follow, world pickup trigger/consumption, normal weapon firing/ammo consumption, alien navigation, contextual-Jump Hoist, safe landing/input restoration, Beam fade-out and the existing faded-renderer silhouette pass. Disposable saves were used; runtime fixture changes were not saved.
- Terrain checks compared exact height samples, painted texture weights, detail-layer placement, vegetation dimensions/colors and tree instances against the originals. All passed.
- Mesh checks confirmed exact triangle positions, winding, normals, tangents and original UVs for all 173 modified embedded meshes plus the cement-bag mesh. Only lightmap UVs, seam vertex duplication and index ordering changed. Existing scene script/collider data and authored transforms are unchanged; the only hierarchy addition is the probe group.

Static shadows are now baked and stable. Small grass and dropped items no longer cast realtime shadows. Baked indirect lighting/color spill and rebaked reflections differ from the previous unbaked ambient approximation; enclosed wall faces are darker, while exposed surfaces and character/ground separation remain readable. Deep recesses can still be very dark. This is a provisional bake: interior fill and final shadow softness deserve an art review when geometry/fixed-light placement stabilizes. No new realtime fill lights were added to disguise that difference.

The excavators are excluded from the bake, avoiding a new permanent arm/bucket shadow. Their future moving sequence itself was not exercised or changed. Existing Beam geometry/colors/spirals and occlusion logic/shaders/features are untouched. The substation silhouette measurement remains one mask draw / 2,120 triangles.

No Android/device build or testing was performed. Existing unrelated MobileAimSettings and Editor JobTempAlloc warnings were not changed or suppressed. No claim of phone frame-time improvement is made from Editor statistics alone.

Local review evidence is under `Logs/ConstructionLighting/`: `Before/`, `After/`, `comparison.csv`, `Smoke/checks.txt`, `terrain-safety.txt`, `mesh-safety.txt`, bake logs and final compile log. Temporary review scripts are archived there, outside Assets; they are not runtime dependencies. The permanent utility is `Assets/Game/Editor/ConstructionSiteLighting.cs`.

Gameplay, camera presets/logic, combat, AI, inventory, Knowledge, missions, save/persistence, Beam, occlusion and UI code were not modified. The user's existing ConstructionSite edits and ProBuilder settings were preserved; reverted railing changes were not restored.

## Exact file manifest

See [ConstructionSiteLightingFiles.txt](ConstructionSiteLightingFiles.txt). It lists all changed/new production files, including Unity metadata and generated lighting outputs. The pre-existing user change to `ProjectSettings/Packages/com.unity.probuilder/Settings.json` is excluded from this pass's manifest.
