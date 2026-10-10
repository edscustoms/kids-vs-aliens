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
No cheaper Medium shader is available. ConstructionSite already uses the standard
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

`SceneGraphicsSettings` owns explicit Terrain/Volume references and reversible
instance overrides. Medium owns a cloned volume profile with bloom disabled.
Disabling/unloading restores references and destroys only owned clones. Gameplay
scene repair wires this component for future levels; no scene-name branching or
per-frame world scans are used. For newly added terrain/volumes, rerun repair.

URP **Strip Unused Variants** is disabled so build-time inference cannot strip
runtime shadow ON/OFF variants. `GraphicsSettingsBuildCheck` fails a build if this
guard is removed. This conservative policy can increase build time/size; replacing
it with selective retention requires build/device verification. Original pipeline
shadow capabilities, lights, shaders and lightmap settings are unchanged.

`DeveloperFpsHud` is compiled/created only in Editor or Development Builds, on the
persistent graphics owner. It samples real elapsed time at 0.5-second intervals,
uses the screen safe area and has no input/raycast surface. Release builds contain
neither this counter implementation nor its UI.

Focused tests: `GraphicsSettingsTests`, `InGameMenuTests`, `SceneCrossoverPlayTests`.
Run through the isolated test runner described in `Assets/Game/Tests/README.md`.
Editor tests do not establish Android shader stripping, visual or performance parity.
