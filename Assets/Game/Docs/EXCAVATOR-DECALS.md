# Excavator mesh decals

`PF_Excavator_A.prefab` contains 17 authored quads under
`Excavator_A_10/UpperPivot/Decals/Branding` and `Decals/Safety`
(34 triangles, two shared atlas materials). The FBX has 12 material-group meshes;
the four existing colliders and root `CameraOcclusionGroup` are retained.
There is no runtime placement component, projector, billboard or mesh intersection.

The shared source is `Assets/Game/Art/Environment/Machinery/FBX/Excavator_A_10.fbx`.
`UpperPivot` is a neutral parent at the existing slew-bearing center, originally
`(1.368769, -0.010653, 0.815091)` in Blender's model-local Z-up frame. Unity's
unchanged 1.7 importer scale/X conversion yields local `(-2.326908, -0.018110, 1.385655)`.
Yaw uses **local Z**, because the existing model root supplies the upright correction.
Its authored rotation is identity. Nine original upper meshes and all decals follow it;
`08_-_Default`, `14_-_Default` and `20_-_Default` remain stationary siblings.
The shared prefab has no animation or runtime controller. Other hinges are unchanged.

The pivot repair preserved the original FBX geometry/material records and importer
metadata, including GUID. Decals were reparented with their world poses preserved.
ConstructionSite's finale instance already had a -0.032 m local-Z override on
`17_YellowPaint`; its three position overrides were translated to the new parent/reference
to retain that exact authored offset. Both scene instances remain in their original poses.

ConstructionSite now has one scene-only `ExcavatorMotionController` on
`_World/LevelGeometry/ConstructionSite_Blockout_Astra/PF_Excavator_A`.
The previous slew pivot was rechecked against the bearing geometry and remains correct;
this swing pass does not modify the FBX or shared prefab. The instance's existing chassis
tilt also tilts its bearing normal; local Z is still the mechanical slew axis.

`PlaySwing()` explicitly starts the proof sequence; scene load never starts it. Start/target
angles are 0/-166 degrees, with a 6-second eased rotation. At 86% of swing time (5.16 s,
approximately -157.15 degrees), the bucket reaches the original striped concrete meshes.
The six existing pieces under `06_GateArea_LockedPlaceholder/ExitBlocker` now move
independently toward six sibling `BlockerClearedPoses/Clear_12` through `Clear_17`
markers. Their start transforms are unchanged. Six `BlockerFlightControls/Kick_*`
markers author independent quadratic kick/drop paths, with 0-0.08 s stagger and
0.57-0.72 s durations. Motion starts with an immediate outward velocity, drops toward
the rubble pose, then settles the last 4 cm during the final tenth of the fall.
The near-left slab now lands farther beyond the bucket; the central walking corridor
is preserved. Paths and rotations are deterministic, with no physics simulation.

At impact all six striped-piece colliders disable before any visual moves. One authored
`ExitBlocker/TemporaryExitBlocker` BoxCollider remains stationary and blocks Amy until
all pieces settle. At that endpoint the six original piece colliders re-enable at their
final visible poses, then the temporary barrier disables. Rubble stays solid; the authored
central corridor remains clear. Reset restores the exact six start poses, original piece
collision and temporary barrier. No collider geometry is rebuilt or cooked at runtime.

Only this finale instance replaces its three stationary upper boxes with six primitive
boxes under `UpperPivot/MovingCollision`: upper house, cab, stick, two boom segments
and bucket. One kinematic body groups them; parent rotation moves collision automatically.
The original track box remains stationary. The inherited upper boxes are disabled by
scene overrides. The shared prefab, FBX and second excavator retain their original setup.

The rubble clears at 5.925 seconds (0.765 s after impact); swing completion still
waits until 6 seconds, after both motions finish. Moving/completed calls
are ignored. Scaled time respects pause. The Inspector exposes the pivot/axis, angles,
swing ease, impact time, per-piece delay/duration, flight-control and clear markers, plus an optional
completion event. Editor buttons capture the current start and preview half/impact/target;
**Reset To Start** restores authored poses and collision. Use **Play Test Swing** in
Play Mode. Reset any edit-mode preview before saving. This proof deliberately adds no
mission, automatic trigger, repair logic, player-control ownership or Active Run participant.

Use **Tools > Environment > Excavator Decals**:

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

`Environment/Mesh Decal` uses alpha clipping, outward normals/back-face culling,
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

- `ExcavatorPivot-01`: **11/11** focused `ExcavatorDecalTests` passed, covering
  the bearing center, fixed tracks and attached upper meshes/labels at 0, +45,
  +90 and -45 degrees, plus separate sides/UV winding, surface
  support, missing-label repair/manual-transform preservation, repeat rebuilds,
  every body/decal renderer in one fade group, outline opt-out, shader compilation, and
  actual rendered fade-to-zero. Available under Test Runner category
  `ExcavatorDecals` (render checks require a graphics-capable Editor).
- Before/after import comparisons of both ConstructionSite instances retained exact
  mesh vertex/normal/tangent/UV/index hashes, mesh IDs and material references.
  Maximum world-vertex rounding error was 0.0000043 m. Both instances passed the
  four-angle articulation check; production pivot rotations remain exactly zero.
  Source importer metadata is byte-identical. Captures and comparison evidence are
  under `Logs/ExcavatorPivot`; the completed temporary validation helper was removed.
- Actual Unity camera renders are in `Logs/ExcavatorDecals/`. A zero-fade render
  is compared pixel-by-pixel with all renderers disabled.
- Original swing proof: `ExcavatorSwing-01` passed 13/13 focused decal/swing checks;
  `ExcavatorSwing-02` passed both swing checks with the extended exit walk, and
  `ExcavatorSwing-03` passed the final authoring/static-rendering check. The Play Mode
  test first confirms the original barrier stops Amy, then drives her normal input/
  `ThirdPersonController` through the cleared gate and around the visible rubble onto
  open ground near `(31.51, -0.42, 56.30)`. Repeated calls, exact reset, completion ordering,
  rigid upper motion and the unchanged second excavator are covered. Actual mesh ray
  crossings at the authored impact and start/mid/impact/fall/end/top/bearing captures are
  in `Logs/ExcavatorSwing`. No full regression or device performance run was requested.


- Collision/rubble correction: `ExcavatorCorrection-03` passed **3/3** focused checks;
  `ExcavatorCorrection-01` passed all **11/11** adjacent decal/pivot checks. The early
  exact-time preview failure was fixed by sharing the completion endpoint; runtime
  capsule and walkthrough checks passed in every run. Coverage includes unchanged
  pivot/swing tuning, primitive collision following at each pose, the actual Amy capsule
  blocked before/midway/after swing, separate delayed piece motion, exact reset,
  unchanged static excavator and normal locomotion through the central rubble corridor.
  Start/impact/fall/clear renders, gameplay captures and a scene-record preservation
  audit are under `Logs/ExcavatorCorrection`. Only four existing scene records changed;
  all six original barrier poses, shared prefab and FBX were preserved in this correction.
  Review the staggered fall's feel in Play Mode and collision on device; these Editor
  checks are not device validation.


- Snappy impact pass: `ExcavatorImpact-01` passed **4/4** focused checks. Coverage
  includes collision removal exactly at impact, sharp initial kick, short authored
  paths, temporary blocking while Amy walks against the fall, safe normal exit,
  final rubble/upper-collider separation and exact reset. Existing upper collision,
  swing/pivot and static-excavator checks remain in this focused selection. The scene
  audit confirms unchanged swing fields and excavator collider records. Actual Unity
  renders at 30 samples/second, playback and gameplay captures are in
  `Logs/ExcavatorImpact`; `git diff --check` passed. The temporary authoring helper
  was removed. Impact feel and device collision remain manual acceptance checks.

- Settled rubble collision: `ExcavatorRubbleCollision-01` passed **5/5** focused tests.
  The six existing mesh colliders restore after settling, before the temporary exit
  box opens. Tests walk Amy on the settled pieces, verify side contact/no penetration,
  traverse the central exit with rubble collision enabled, and retain swing/upper
  collision/reset checks. Scene, shared prefab and FBX hashes are unchanged by this
  correction. `git diff --check` passed; device collision remains a manual check.

Remaining acceptance checklist:

1. In ConstructionSite Play Mode, walk Amy behind/around the excavator and verify
   fade-out, hold and restoration with the real gameplay camera and existing lines.
2. Rotate the gameplay camera at near/far zoom and inspect both boom/body sides,
   rear and service panel, including grazing angles and shadows.
3. On Android, repeat the camera/fade checks and profile a representative number
   of excavators. Confirm the source materials' existing URP/Lit fallback as well
   as the new decals; Editor results do not verify mobile shader stripping.
