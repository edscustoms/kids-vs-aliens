# Excavator mesh decals

`PF_Excavator_A.prefab` contains 17 authored quads under `Decals/Branding` and
`Decals/Safety` (34 triangles, two shared atlas materials). The existing FBX,
10 render groups, four colliders and root `CameraOcclusionGroup` are retained.
There is no runtime placement component, projector, billboard or mesh intersection.

Use **Tools > Kids VS Aliens > Environment > Excavator Decals**:

- **Create Missing Decals (Preserve Existing)** adds absent labels. Existing
  transforms, meshes and hierarchy groups are preserved.
- **Rebuild Decals from Authored Layout** reapplies the saved layout's positions,
  rotations, sizes, UVs and material defaults to the managed label paths. Existing
  object/asset identities are reused; unrelated children are retained. This is the
  explicit operation that overwrites manual decal transforms.
- **Select Authored Layout** opens `Assets/Game/Editor/ExcavatorDecalLayout.asset`.
  Edit this asset when a placement should survive future rebuilds. Each rectangle
  uses pixels measured from the top left of the original 1448 x 1086 atlas.
- **Capture Camera Angle Review** writes 24 perspective orbit views (12/35/60
  degree elevations) and six close-ups to `Logs/ExcavatorDecals/`.

The create/rebuild commands target the named prefab asset. When that asset is open
in Prefab Mode they edit the visible stage; save it normally. Otherwise they load,
update and save only that prefab. No gameplay scene setup step is required.

For a quick manual adjustment, open the prefab and move/rotate/scale the individual
label child. Avoid negative scale: each physical side already has correctly wound
geometry and readable UV orientation. The Decals root follows the FBX's authored
frame at creation/rebuild: +X front/bucket, +Y up, +Z left/cab side. Source import
scale is 1.7. Replacing/rescaling/reposing the source geometry requires reviewing
the authored layout; the tool deliberately does not search for new surfaces.

`KVA/Environment/Mesh Decal` uses alpha clipping, outward normals/back-face culling,
queue 2475 (`AlphaTest+25`), `ZTest LEqual`, `ZWrite On`, and `Offset -1,-1`.
Measured surface offsets are 2–3 mm, with a small tolerance for the rear facets.
Lighting is main-light diffuse/shadows plus light probes; no additional-light loop,
normal map, projector buffer, shadow-caster or depth-normal pass is added.
See Unity's [depth-test reference](https://docs.unity.cn/6000.3/Documentation/Manual/SL-ZTest.html)
for the depth-test semantics.

The shader's `_Fade` is always compiled and uses the same screen-space Bayer
threshold as `SG_EnvironmentSurface`. The existing controller includes the labels
through `CameraOcclusionGroup`; its fade timing, collider detection and aim semantics
are unchanged. A shader tag opts surface decoration out of structural outline
generation, so faded labels do not leave rectangular dashed boxes.

The current controller still creates/caches runtime fade materials, and several
excavator source materials still use stock URP/Lit's existing transparent fallback.
This task does not migrate shared environment materials or redesign that controller.
The decal shader itself does not require runtime transparency keyword switching.

Validation (Unity 6000.5.6f1, Editor GPU):

- New runtime/editor code compiled without new warnings; the project already has
  warnings in `ElectricGrenadeBurstVFX` and `GameplaySceneSetup`.
- Six focused `ExcavatorDecalTests` checks cover separate sides/UV winding, surface
  support, missing-label repair/manual-transform preservation, repeat rebuilds,
  all 27 renderers in one fade group, outline opt-out, shader compilation, and
  actual rendered fade-to-zero. They are available under Test Runner category
  `ExcavatorDecals` (render checks require a graphics-capable Editor).
- Actual Unity camera renders are in `Logs/ExcavatorDecals/`. A zero-fade render
  is compared pixel-by-pixel with all renderers disabled.

Remaining acceptance checklist:

1. In ConstructionSite Play Mode, walk Amy behind/around the excavator and verify
   fade-out, hold and restoration with the real gameplay camera and existing lines.
2. Rotate the gameplay camera at near/far zoom and inspect both boom/body sides,
   rear and service panel, including grazing angles and shadows.
3. On Android, repeat the camera/fade checks and profile a representative number
   of excavators. Confirm the source materials' existing URP/Lit fallback as well
   as the new decals; Editor results do not verify mobile shader stripping.
