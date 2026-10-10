# Graphics settings

`GraphicsPreferences` owns two device PlayerPrefs keys (`settings.graphics.quality`
and `.shadows`). They are independent of Active Run and permanent progression.
Defaults are High and Shadows ON. Both menu Options screens use `GraphicsOptionsView`.

Tune `Assets/Game/Resources/GraphicsQuality.asset`. Multipliers are relative to the
platform's original URP asset and each scene's authored terrain settings. High is
configured as the identity: original material, distances, geometry and post effects.

| Setting | Low | Medium | High |
|---|---:|---:|---:|
| Render scale multiplier | .75 | .875 | 1 |
| Effective Mobile / PC scale | .6 / .75 | .7 / .875 | .8 / 1 |
| Terrain pixel error multiplier | 4 | 2 | 1 |
| Effective BikeRoute / ConstructionSite error | 8 / 20 | 4 / 10 | 2 / 5 |
| LOD bias multiplier | .5 | .75 | 1 |
| Terrain tree/detail distance multiplier | .5 | .75 | 1 |
| Camera far clip cap | 250 m | 375 m | authored |
| Shadow distance with Shadows ON | 30 m | 40 m | 50 m |
| Post processing | off | bloom off; other effects retained | original |

Material slots map an original Terrain material to optional Low/Medium materials.
High always uses the original. Missing/unsupported replacements fall back to it.
BikeRoute Low uses the existing URP Terrain/Lit shader with the Terrain's authored
layers; it deliberately omits custom ground treatment, cliff projection and moss.
Medium uses an independent, initially identical copy of the custom terrain shader:
`Assets/Game/Scenes/BikeRoute/Environment/Medium/BikeRouteTerrainMedium.shader`,
with `BikeRouteGroundMedium.hlsl` and `BikeRouteForestMossMedium.hlsl` in that same
folder. `Terrain Medium.mat` preserves the original property and texture references.
Edit these Medium files for future optimization; High still uses the original files.
No Medium shader optimization has been applied. The Medium preset's other reductions
(render scale, geometry, LOD and post) still apply, so compare shader appearance with
those settings held constant. ConstructionSite already uses the standard
Terrain material and has no separate shader tiers. No new shader was implemented.
Layer-specific object draw distances are configurable but opt-in: the shipped
layer mask is empty to avoid hiding gameplay objects sharing Default. Camera far
clip and LOD bias still reduce object/vegetation rendering distances.

`GraphicsSettingsController` is bootstrapped once before scene loading, persists
across scenes, and owns a private URP asset. Render scale and shadow distance never
mutate shared assets. Shadow distance zero gates both URP shadow-caster passes;
mixed/baked lighting remains enabled. Camera limits apply only during Game-camera
rendering and are restored afterward, preserving Cinemachine's authored lens.
RenderTexture preview cameras and Scene View are excluded.

The controller restores its original quality slot before releasing the temporary
pipeline on disable, Play Mode exit or assembly reload. Editor cleanup also clears
SRP's cached build-asset list, which can otherwise retain the destroyed clone.
Build preflight repeats this before Core/URP preprocessing. Script reload recreates
the runtime override without adding another FPS HUD.

`SceneGraphicsSettings` owns explicit Terrain/Volume references and reversible
instance overrides. Medium owns a cloned volume profile with bloom disabled.
Disabling/unloading restores references and destroys only owned clones. Gameplay
scene repair wires this component for future levels; no scene-name branching or
per-frame world scans are used. For newly added terrain/volumes, rerun repair.

URP **Strip Unused Variants** stays enabled. Presets change render scale and
shadow distance, not pipeline feature support. With distance zero, URP renders no
shadow casters and binds an empty shadow map using its retained shadow variant;
normal lighting and mixed/baked masks remain supported. Do not disable pipeline
shadow capabilities at runtime without revisiting build retention.

Resources/GraphicsQuality directly references all runtime terrain materials, retaining
High/Medium/Low shaders and their material keywords. No broad Always Included Shader
entry or variant collection is needed. Build preflight validates these references and
enabled stripping. Enable Player > Shader Settings > Strict shader variant matching
for Development validation, then restore the prior flag for normal builds.
`PostProcessingAlphaVariants` is a metadata-only renderer feature on the Mobile
and PC renderers; it adds no render passes. Unity 6000.5's alpha-output capability
filter otherwise retains only alpha ON, while opaque Android displays request
OFF (including Bloom/UberPost). Its narrow keyword override retains both states
without disabling alpha output or unused-feature stripping. Build preflight checks
this when a pipeline supports alpha output. Use
`GraphicsSettingsSetup.EnsureAlphaOutputVariants` when authoring such a renderer.
All-shader variant logging and JSON export are enabled; inspect Editor.log and
Temp/shader-stripping.json after building. New renderer features must be
represented by the platform's build-time URP assets, then checked in a strict player.

`DeveloperFpsHud` is compiled/created only in Editor or Development Builds, on the
persistent graphics owner. It samples real elapsed time at 0.5-second intervals,
uses the screen safe area and has no input/raycast surface. Release builds contain
neither this counter implementation nor its UI.

Focused tests: `GraphicsSettingsTests`, `InGameMenuTests`, `SceneCrossoverPlayTests`.
Run through the isolated test runner described in `Assets/Game/Tests/README.md`.
Editor tests do not establish Android shader stripping, visual or performance parity.
Strict Android validation passes on Nord 3 Vulkan. Its OpenGL ES driver rejects
current terrain variants at link time (17–23 fragment samplers versus its limit of
16), even when the exact variants are retained. GLES terrain compatibility remains
unresolved; disabling strict matching or retaining more variants does not establish
a fix. Resolve sampler usage in a separately reviewed shader compatibility change.
