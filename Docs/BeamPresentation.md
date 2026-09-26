# Shared beam presentation and fresh arrival authoring

Historical validation results and changed-file lists below describe their original passes. Some one-off helpers have since been retired; see [helper cleanup](LegacyHelperCleanup.md). Current automated validation uses the [Tests README](../Assets/Game/Tests/README.md).

Both automatic arrival and manual Hoist use `Assets/Game/Prefabs/PF_BeamTransportVFX.prefab`.
The cone/core/middle/ground meshes, vertex data, dimensions and mesh transforms are preserved.
Hoist paths, follow behavior, travel timing and validation are unchanged.

## Hoist materialization and dissipation

Tune the root **BeamTransportVFX** component on `Assets/Game/Prefabs/PF_BeamTransportVFX.prefab`:

- **Hoist Fade In Duration:** 0.35 seconds.
- **Hoist Fade Out Duration:** 0.55 seconds.

Contextual Jump acquires the existing transport lock immediately and stops footstep/foley playback.
Amy stays at the start while the complete Beam materializes. The unchanged travel clock starts on
the next frame after visibility reaches one. Landing releases the lock immediately; the Beam stays
at its last position and dissipates independently while Amy can move. The existing input router's
one-frame resume/held-input neutral guards remain intact. Cancellation and disable clear either fade.

One renderer property drives the cone, four authored spirals, motes and lit core/middle/ground.
`BeamLitFade.shader` reuses URP Lit's forward lighting and attenuates its complete premultiplied
output, including emission and specular. At full visibility its captured output is byte-identical
to the original Lit shader. No material color, geometry, particle or lighting settings were retuned.
Arrival retains its immediate Show, descent and landing hold, then uses the same landing fade-out
as Hoist when controls are released. Departure retains its immediate Show/Hide behavior.

Unity 6000.5.6f1 validation: `BeamPresentationReview.RunTiming` exercised two real contextual-Jump
Hoists in ConstructionSite, stationary/silent fade-in, exact Bezier positions, movement during
independent fade-out, reuse, fade-in cancellation and unchanged fresh arrival. Gameplay-camera
frames were visually reviewed in `Logs/BeamRefinement/timing-*.png`. Focused Editor results:
20/20 passed (`Logs/BeamTimingTests.xml`), including Floating and directional VFX regressions.

## Arrival

Select the root `LevelStart` in ConstructionSite. `BeamInSpawn` has been removed.
Its `BeamArrivalPoint` component draws the arrival position and facing arrow. Move the root Transform
to the desired **player root** endpoint and rotate its Y angle for facing. The capsule and descent
path must be unobstructed; fresh arrival does not require Hoist's short ground-support probe.
An above-ground root hands off at exactly that pose, then normal gravity settles Amy onto the floor.
The beam now uses this same endpoint instead of its independently positioned VFX root.

`PlayerBeamInSequence` still owns the existing start-height/delay/duration/hold configuration.
Its existing restore bypass remains intact: Continue restores the saved pose and does not play arrival.
`Tools > Setup > Setup or Repair Active Gameplay Scene` repairs components/references through
`BeamTransportSetup`. It preserves the root even if renamed, and never resets its pose.
New LevelStart instances use the root of `PF_LevelStart`. See `LevelStartAuthoring.md` for the
root-coordinate migration and current authoring validation; older review results below are historical.

## Visual tuning

- `PF_BeamTransportVFX/BeamInVFX` → **BeamEnergyField**: spiral turns, rotation speed, ribbon width,
  radius fraction and particle radius fraction. The hidden cone envelope is sampled from existing
  authored vertices, not an alternate transport size.
- `BeamInVFX/BeamSparks` → standard **ParticleSystem**: emission (55/s plus 120 initial motes),
  maximum 320 particles, size/lifetime/color ranges. Local vertical velocity is authored Down;
  `BeamTransportVFX.SetDirection` reverses it for Up.
- `M_BeamCore` / `M_BeamMiddle`: existing material color/emission controls.
- `M_BeamOuter`: violet volume and cyan edge-support tint/opacity, using `Game/Beam Volume`.
- `M_BeamSpiral` / `M_BeamMotes`: HDR emission via `Game/Beam Energy`.
- `EnergySpiral` → **LineRenderer**: color and width gradients. BeamEnergyField owns positions and
  overall width. One ribbon, shared materials, reused position/particle arrays, no realtime lights.

The explicit `Tools > Setup > Beam Transport > Apply Refined Energy Presentation` command reapplies
the reference art preset. Routine gameplay repair does **not** invoke this command or overwrite art tuning.

## Unity review

The retired one-off review used isolated temporary saves and unsaved marker edits for two fresh
arrival poses, three contextual Hoists and a real Quit/Continue cycle. Its historical HDR captures
are in `Logs/BeamRefinement`. Current `BeamPresentationTests` checks directional particle containment/cleanup and
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
