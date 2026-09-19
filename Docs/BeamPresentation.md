# Shared beam presentation and fresh arrival authoring

Both automatic arrival and manual Hoist use `Assets/Game/Prefabs/PF_BeamTransportVFX.prefab`.
The cone/core/middle/ground meshes, vertex data, dimensions and mesh transforms are preserved.
Hoist paths, follow behavior, timing, validation and input are unchanged.

## Arrival

Select `LevelStart/BeamInSpawn` in ConstructionSite (the existing marker name is retained).
Its `BeamArrivalPoint` component draws the arrival position and facing arrow. Move its Transform
to the desired **player root** endpoint and rotate its Y angle for facing. The existing supported,
unobstructed landing/path requirements still apply. No runtime snap to another coordinate occurs.
The beam now uses this same endpoint instead of its independently positioned VFX root.

`PlayerBeamInSequence` still owns the existing start-height/delay/duration/hold configuration.
Its existing restore bypass remains intact: Continue restores the saved pose and does not play arrival.
`Tools > Setup > Setup or Repair Active Gameplay Scene` repairs marker/component references through
`BeamTransportSetup`. It preserves the referenced marker even if renamed, and never resets its pose.
New LevelStart instances inherit the marker from `PF_LevelStart`.

## Visual tuning

- `PF_BeamTransportVFX/BeamInVFX` → **BeamEnergyField**: spiral turns, rotation speed, ribbon width,
  radius fraction and particle radius fraction. The hidden cone envelope is sampled from existing
  authored vertices, not an alternate transport size.
- `BeamInVFX/BeamSparks` → standard **ParticleSystem**: emission (55/s plus 120 initial motes),
  maximum 320 particles, size/lifetime/color ranges. Local vertical velocity is authored Down;
  `BeamTransportVFX.SetDirection` reverses it for Up.
- `M_BeamCore` / `M_BeamMiddle`: existing material color/emission controls.
- `M_BeamOuter`: violet volume and cyan edge-support tint/opacity, using `KVA/Beam Volume`.
- `M_BeamSpiral` / `M_BeamMotes`: HDR emission via `KVA/Beam Energy`.
- `EnergySpiral` → **LineRenderer**: color and width gradients. BeamEnergyField owns positions and
  overall width. One ribbon, shared materials, reused position/particle arrays, no realtime lights.

The explicit `Tools > Setup > Beam Transport > Apply Refined Energy Presentation` command reapplies
the reference art preset. Routine gameplay repair does **not** invoke this command or overwrite art tuning.

## Unity review

`BeamPresentationReview.Run` uses isolated temporary saves and unsaved marker edits for two fresh
arrival poses, three contextual Hoists and a real Quit/Continue cycle. It produces HDR captures in
`Logs/BeamRefinement`. `BeamPresentationTests` checks directional particle containment/cleanup and
marker repair in ConstructionSite and GamePoc. The existing arrival regression also checks two poses.
No device validation is part of this pass.

Verified in Unity 6000.5.6f1: 12/12 focused Editor regressions; fresh arrivals at two distinct
scene-marker positions with 37° and 143° facing; three Jump-activated Hoists with the original
Bézier and grounded X/Z-follow behavior; rotating ribbon; particle cleanup/reuse; and an actual
Quit/Continue restoring the saved player pose with no arrival. Both scene repair fixtures pass.
Post-processed arrival and Hoist captures were visually inspected. Baseline comparison confirms
all 14 existing mesh/transform/ProBuilder geometry records are unchanged (`Logs/BeamRefinement/geometry.txt`).

Changed production files: `BeamEnergyField.cs`, `BeamArrivalPoint.cs`, the arrival VFX anchor in
`BeamTransportController.cs`, an Inspector tooltip in `PlayerBeamInSequence.cs`,
`BeamTransportSetup.cs`, `PF_LevelStart.prefab`, `PF_BeamTransportVFX.prefab`, three existing beam
materials, two new energy materials, and `BeamEnergy.shader` / `BeamVolume.shader`.
Supporting files: the explicit `BeamPresentationSetup` preset, `BeamPresentationReview`,
`BeamPresentationTests`, and the existing arrival regression in `BeamTransportTests`.
No scene placement, persistence, camera, UI, input, hoist baking or path assets were changed.
