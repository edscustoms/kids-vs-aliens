# Grenade release animation integration

Implemented 6 September 2026 using the existing player animation facade.

## Runtime flow

`Held → Charging → ReleaseThrow → Throwing → authored GrenadeRelease marker → physical throw → Idle`

Releasing FIRE snapshots `Charge01` and requests `CharacterActionId.GrenadeThrow`
through `PlayerAnimation`. Until release, the held visual stays attached, the
weapon remains equipped but hidden, firearm input stays blocked and inventory is
unchanged. The marker invokes the existing prepare/consume/launch/cleanup path.
Required Knowledge, inert recovery/feedback, owner collision ignores, upward bias,
spin and equipment/ammo/reload state keep their existing behavior.

The release path and marker subscription are guarded against duplicate calls.
Cancel, disable, real equipment changes and character replacement invalidate the
pending request. The animation facade detaches the old character relay. A new
request cannot adopt markers from a cancelled animation still playing on that
character; it uses the immediate fallback until that performance has ended.

## Authored marker and shared action mapping

`GrenadeThrow_Unarmed.anim` calls only
`CharacterAnimationEventRelay.OnCharacterAnimationEvent(AnimationEvent)`, with
`intParameter = CharacterAnimationEventId.GrenadeRelease` (0). The relay is on the
Animator's GameObject and emits a typed marker with the originating state hash.
`PlayerAnimation` checks it against the requested action and forwards the typed
event to the interested gameplay controller.

`HumanoidAnimationActions.asset` maps `GrenadeThrow` to trigger `ThrowGrenade`,
layer `GrenadeThrow`, state `GrenadeThrow.TossGrenade` and the existing generated
clip. These Animator details live in animation data/facade, never grenade gameplay.
The optional clip/state metadata is needed only for actions that wait for markers;
ordinary existing `TryPlayAction(action)` calls remain supported.

The marker is authored at the generator's forward-release key, currently 0.54 s
(`GenerateGrenadeThrowAnimation.ReleaseTime`). This value exists only in Editor
authoring. Replacing/retiming the clip means moving its authored event and updating
its action binding. There is no
`WaitForSeconds` or hardcoded release delay in grenade gameplay.

The existing generator reauthors the marker every regeneration. The corrected
motion is authored in the generator; mask, VFX and physics tuning retain their
existing behavior. An old experimental
Base Layer transition also consumed `ThrowGrenade`; wiring removes that conflicting
transition so the dedicated layer can play over locomotion. The unused experimental
Base Layer state is retained as authored content. Trigger startup defaults to false.

## Fallback and pause

Missing facade, relay, mapping, marker clip, usable Animator or matching state
causes an immediate throw through the same authoritative path. Missing cosmetic
content cannot strand the grenade in Throwing.

If a successfully started animation is interrupted before release, it returns to
Held without throwing or consuming. The player can charge again. The facade also
cancels a broken transition that never enters within the authored clip's duration;
this is cancellation protection, not release timing. A state that finishes without
its marker is cancelled as well.

Pause preserves a committed Throwing state and freezes the normal Animator.
Resume continues the wind-up. A marker delivered during the pause boundary is
deferred until gameplay resumes. Held/Charging pause behavior remains unchanged:
selection survives and charging is cancelled to Held, requiring fresh FIRE.

## Editor setup

Run **Tools → Kids VS Aliens → Helpers → Wire Grenade Throw Animation** to repair
the marker/mapping and relays without regenerating poses or saving gameplay scenes.
The existing **Generate Grenade Throw Animation** command includes the same wiring.
Both existing compatible character prefabs (Amy and SportyGranny) share the mapping.
`CharacterSetupHelper` configures the relay and default compatible mapping when
creating future characters. Existing authored custom mappings are preserved.

Future custom controllers can supply their own `CharacterAnimationActions` asset,
including the action's trigger, layer, full state path and marker-bearing clip.
Put the generic relay on their Animator GameObject. No character name is hardcoded
in grenade runtime code. Knowledge previews allow this passive relay and continue
to disable Animator events, so they cannot throw gameplay grenades.

## Readable throw motion (6 September update)

The original generator held idle through 0.28 s and barely lifted the hand. It now
reproduces an eight-key Humanoid motion using the imported idle as the pose basis:

- 0?0.32 s: torso winds back, right elbow moves behind/up, forearm folds beside the head.
- 0.32?0.54 s: shoulder and upper arm drive forward; the forearm unfolds into a
  near-straight forward reach. The typed release event is at 0.54 s.
- 0.54?0.95 s: follow-through lowers the extended arm and unwinds the torso.
- 0.95?1.35 s: eased recovery; the last 0.25 s also blends back into the current
  underlying locomotion through the existing layer's Idle transition.

Clamped-auto muscle curves replace the old linear curves. The left arm provides
smaller counterbalance. No transform/root/leg animation or runtime bone correction
was added. Repeated generation produced an identical clip (SHA-256
`4DB6C3A5700479B16C9398E2181743CF254A09C9B17654170501A505FA78194F`).
The generator saves only its relevant assets, avoiding a global dirty-asset save.

## Visual review

Rendered and inspected the actual Amy and SportyGranny prefabs with
`HumanoidShooter.controller`, `WeaponStyle = 0`, and the real held grenade in an
isolated Editor preview scene in this existing project. Reviewed idle, half forward
input, full forward, both strafes, both forward diagonals, backward, and both
backward diagonals, from three-quarter and right-side views. Legs keep animating
beneath the throw. The final silhouette clearly shows preparation, elbow drive,
forward extension, follow-through and return to the moving base pose.

`PlayerAnimation` currently normalizes nonzero velocity: walking and running send
the same direction to this blend tree. Full forward captures therefore cover their
shared animation contract; half input is an additional blend-tree check, not a
claim that runtime currently selects a separate walk clip. That movement contract
was not changed by this animation task.

Reproduce key-pose captures with **Tools > Kids VS Aliens > Helpers > Capture Grenade
Throw Motion** or its **(Side)** variant. Output goes under `Logs/GrenadeMotion`.
The helper samples the real Animator and CPU-bakes each evaluated skinned pose
because repeated captures inside one Editor frame otherwise reuse GPU skinning.
It uses an isolated preview scene, cleans up its objects, and saves no gameplay scene.
It disables gameplay events and hides the held preview after the authored marker;
physical release is checked separately by the native-event integration tests.

This pass also saved a 30 FPS playback/scrubbing viewer at
`Logs/GrenadeMotion/review.html`, using `final-dense` and `final-dense-side` strips.
Key-pose contact sheets and measurements are in `generate-final` and
`generate-final-side`. These local review artifacts are ignored by Git.

## Verification

Runtime and Editor C# compilation passed. **18/18 focused tests passed** in
`Logs/GrenadeMotion-Focused-tests.xml`, including native event delivery and launch
from the extended hand at 30 and 60 FPS. The two motion tests each exercise ten
locomotion inputs on their real character rig: raised wind-up, elbow behind the
shoulder, forward reach above 85% of arm length, release within 8% of arm length of
maximum reach, preserved foot motion, and recovery to the current base hand pose.
These are motion constraints, not assertions that duplicate generator curve values.

The broader Core run was **55/59 passed** (`Logs/GrenadeMotion-Core-tests.xml`,
before adding the two native release-position cases). Four pre-existing coroutine
tests failed because `Time.frameCount` stayed at 1 in this open Editor session,
even with queued player-loop updates and view repaints: suspension neutral-input
resume, held-grenade suspension, charging-grenade suspension, and Knowledge modal
sequencing. A repeat without resetting a leaked paused clock also failed the legacy
melee resume test; resetting the temporary runner's clock restored that test.
No gameplay or existing tests were changed to hide these harness failures.
The earlier integration-only validation passed 57/57 in the existing-project batch
runner. A clean batch rerun remains advisable when the open Editor can be closed.
No temporary Unity project or Library rebuild was used. Temporary automation was
removed; the reusable capture menu remains.

Manual Play Mode/device acceptance still needs:

- Actual game-camera readability at the intended distance, with mobile FIRE input,
  while idle, walking/running, strafing and moving diagonally.
- Pistol/rifle return poses, repeated throws, minimum/maximum charge, cancel/equip/
  switch before release, and pause during charge, wind-up and recovery.
- Inert grenade feedback/recovery and known-skill effects on device.

No enemy animation code, shooting animation integration, grenade effects or input
architecture was changed. Compilation reports the existing unrelated unused
`ElectricGrenadeBurstVFX.radiusScaledRoot` field.
