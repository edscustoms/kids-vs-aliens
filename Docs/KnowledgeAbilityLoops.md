# Knowledge ability demonstrations

`SkillDemoPlayer` still resolves `PlayerCharacter.CurrentCharacterPrefab`, which is
populated from the selected loadout (or the scene's existing fallback). It clones
that visual, its Animator, action bindings and aura; it never animates the live
player. A character-selection change rebuilds the open demonstration.

`KnowledgeAbilityDemo` owns the two presentation-only loops and their cleanup.
It receives unscaled time from the existing demo host while Knowledge owns the
normal gameplay suspension lease.

- Beam: actual `PF_BeamTransportVFX`, `SetDirection`, `BeamHoistPath.Create/Evaluate`
  and `CharacterAnimatorDriver.SetFloating`. One preview-local cubic path is evaluated
  forward and backward. No gameplay controller, validation, zones or input changed.
  Travel takes 1.8 seconds each way; beam-off rests are 0.5 seconds above and 1.0
  second below, for a 5.1-second cycle. Exact endpoint assignment prevents drift.
- Grenade: actual `GrenadeThrow` action and `CharacterAnimationEventRelay` release
  marker, `HeldItemGrip.ReleasePosition`, `GrenadeItemData` launch values and the
  actual thrown prefab's `GrenadeInstance.TryPrepare/Launch` and Rigidbody.
  Physics runs in an isolated, manually stepped PhysicsScene. Gameplay scripts and
  colliders on that instance are disabled. A copy of the same item's held visual
  projects its physical pose into the Canvas-scaled preview rig. This avoids changing
  global physics/time or copying the ballistic implementation.
- Grenade timing: 0.6-second initial stance, authored throw/release, physical flight
  out of frame, 1.5-second off-screen rest, 0.35-second directional cyan light pulse,
  0.8-second reset, repeat. The pulse uses the real electric effect data's color.
  The full damage/burst effect is deliberately not invoked; the requested distant
  activation is represented by light from the throw side only.

Closing, switching, disabling and exceptions unsubscribe the preview marker, clear
Floating, hide/destroy the beam, remove projectile visuals/physics, remove the local
lights and unload the private physics scene. There is no inventory or persistence
dependency in the loop owner.

Tune loop timings/height on `Assets/Game/Data/Presentation/BeamHoistTutorial.asset`
and `GrenadeHandlingTutorial.asset`. Beam framing fits the entire travel envelope
once in `KnowledgePreviewStage.FrameTravel`; it does not move the camera during
the loop. The extra key/flash lights affect only the existing preview rendering layer.
The floor, UI layout and gameplay cameras are unchanged.

`GameplayPresentationSetup.CreateInitialAssets`, already called by canonical scene
repair, wires the Beam tutorial asset and migrates the old idle grenade demo. It
preserves existing tuned loop values on repeated setup.

Changed production files: `SkillDemoPlayer.cs`, `SkillTutorialData.cs`,
`KnowledgePreviewStage.cs`, new `KnowledgeAbilityDemo.cs`, `GameplayPresentationSetup.cs`,
`BeamHoist.asset` (tutorial reference only), new `BeamHoistTutorial.asset`, and
`GrenadeHandlingTutorial.asset` (demo/action selection only), with new asset metadata.

## Unity validation

`KnowledgeAbilityReview.Run` uses isolated fixture saves and the actual Knowledge
presenter in ConstructionSite. Its checks cover three complete cycles per ability,
non-empty grenade inventory remaining unchanged, a stationary real player, authored
release counts, exact beam returns, endpoint framing, requested rest intervals,
repeated Review/Got It, alternate selected character and disable cleanup. Captures
are written to `Logs/ProceduralUI/ability-*.png`; the Unity log is
`Logs/KnowledgeAbilityReview.log`. No Android/device validation is involved.

Result: passed in Unity 6000.5.6f1 Play Mode using Direct3D 11, with a successful
editor exit. Amy and SportyGranny captures were visually inspected. Three cycles
per ability, both characters, repeated reviews, same-frame character replacement,
disable and cleanup passed without runtime exceptions. Measured waits were about
0.50/1.01 seconds for Beam and 1.51 seconds before the grenade flash. The real
player transform and inventory containing three grenades remained unchanged during
the measured loops. An earlier Direct3D 12 run passed the assertions but crashed
on editor shutdown; the two subsequent Direct3D 11 runs exited successfully.

The Beam preview is framed wider than the standing tutorials to keep both endpoints
visible. Flash strength and framing remain ordinary art-direction tuning; full
grenade explosion visuals are intentionally excluded from the frame.
