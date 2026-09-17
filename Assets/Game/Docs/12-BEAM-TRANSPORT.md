# Beam Transport V1

`BeamTransportController` owns movement, a lease from the existing
`GameplaySuspensionController`, the CharacterController handoff and one reusable
world-space VFX instance. Arrival, departure and the Knowledge-gated hoist share
this controller. No beam is parented to the player.

`BeamTransportVFX.SetDirection(Up/Down)` switches BeamSparks' Velocity over
Lifetime Y from a cached authored baseline. Constants, random ranges and curves
retain their magnitude/distribution. Repeating a direction never compounds a
sign change. The emitter's existing local rotation is preserved (ConstructionSite
sparks are inverted); the baseline direction is measured in world space.
Other velocity axes, shape, size, colour, materials and emission are unchanged.
The current effect has zero start speed/gravity and its vertical movement comes
from this module. If reauthoring the particles, keep that contract; do not add a
second vertical driver in start speed, force or gravity without extending the
direction adapter. The beam stays vertical during the lateral phase.

## Scene setup

Run **Tools > Setup > Setup or Repair Active Gameplay Scene**. This adds/repairs
the controller, Knowledge-gated ability, VFX prefab reference and HOIST touch
button. Keyboard H uses the existing Starter Assets input action map. Both input
paths respect the existing gameplay input block and fresh-input rules.
The touch button appears only after Knowledge is learned and a target is in range;
it hides during transport. Physics validation still runs on activation.

An authored `LevelStart/BeamInSpawn` opts a scene into automatic arrival. Its
`PlayerBeamInSequence` is now only a startup adapter. At Awake order -150, after
the suspension service (-200) and before normal player initialization, it acquires
ownership through the transport controller (initialized at -180). It places Amy
above the destination synchronously. There is no Start
coroutine or pose reset on subsequent frames. The old script had an empty
`controlsToDisable` array and null CharacterController in the saved scene, plus
two yielded frame resets; neither lock was functioning in that configuration.
The replacement resolves the controller from the actual player and uses the
already-wired suspension consumers.
The ConstructionSite arrival marker was also below the terrain (Y=0 versus
approximately Y=0.42 surface height); it is aligned to the actual surface so the
destination capsule clears the ground.

The helper preserves existing arrival timing and visual transforms. It captures
the authored scene beam into `Assets/Game/Prefabs/BeamTransportVFX.prefab` only
when that asset is absent. Subsequent repairs do not overwrite tuned VFX assets.
ConstructionSite retains its authored arrival effect; hoists/departures reuse a
single instance of that same prefab per player.

## Knowledge and target authoring

Place `BeamHoistBook_Dropped.prefab`, or use `BeamHoistBook.asset` in the existing
item spawner. Reading it in the existing inventory unlocks `BeamHoist.asset` via
`PlayerSkillState`; no secondary progression state is added. The existing project
only persists skills for the current app session, including scene reloads. This
task does not add disk-save persistence or XP rewards.

Add `BeamHoistTarget` at a level-authored activation point below/beside a ledge:

1. Set Activation Range, or assign an activation volume (use a trigger collider).
2. Create/assign a Landing transform at Amy's **root/feet** position on solid,
   walkable geometry. Leave enough room for the full player capsule.
3. Set Clearance Height above the landing surface. An optional Lift Point sets
   a higher world Y; its X/Z are deliberately ignored.
4. Set lift, transfer and landing durations. Selected gizmos show the route.

The route is vertical at Amy's current X/Z, horizontal above the destination,
then vertical down to the exact landing position. Raise the lift point to clear
any parapet higher than the landing surface. The full capsule path and supported
landing are validated against real physics before activation. Each movement step
is swept again, preventing passage through new blockers. A blocked request leaves
control untouched; an interrupted move restores normal control at its last safe
position, allowing normal gravity to resume. Overflow of query buffers rejects
the movement conservatively. Keep the player upright, as in normal gameplay.

Targets register/unregister on enable/disable, so runtime additions work. The
ability selects the nearest valid target in the player's scene when activated.
Before Knowledge, requests fail. Book placement and ledge targets are intentionally
level content; the standard repair command does not populate arbitrary ledges or
grant Knowledge automatically.

## Level exits and interruption

Add `LevelBeamTransportTrigger` to an authored exit trigger. Its completion event
calls the level's existing transition flow. A completed departure holds control
until scene unload; an explicit `CancelTransport()` releases it if a transition
is canceled. Wire the event before using an exit in gameplay.

Transport leases block player input/gameplay consumers without freezing time.
Manual pause/Knowledge modals still freeze the world and transport; releasing
either owner cannot release the other owner's control lock. Disable/destroy
releases transport ownership and restores the original capsule enabled state.
Movement velocity is reset at handoff to avoid carrying pre-hoist gravity/speed.

## Acceptance checks

- ConstructionSite: hold movement/FIRE while entering Play; verify one descent,
  no pre-arrival movement/reset, stationary beam and normal controls afterward.
- Read the book, approach an authored target and use H and touch HOIST. Confirm
  vertical clearance before lateral motion, exact landing and upward sparks.
- Try without Knowledge, out of range, with low ceiling and blocked landing.
- Pause during lift/transfer; resume and disable/unload during transport.
- Repeat up/down use; confirm particle speed/style and direction stay consistent.
- Regress movement, camera, aiming, pistol/rifle, grenade selection/charge and
  Knowledge modal ownership. Check touch placement and lifecycle on Android/iOS.

Editor regression coverage is in `BeamTransportTests` (Core category), including
both ConstructionSite and GamePoc setup idempotence. Compilation, Editor tests,
Play Mode observations and device acceptance must be reported separately.

## Verification — 17 September 2026

- Runtime and Editor assemblies compile against this project's Unity references.
- Existing-project batch Core suite: **110 passed, 0 failed, 0 skipped**.
  Result: `Logs/BeamTransport-Verified-tests.xml`.
- The actual-scene menu test enters Play Mode and observes ConstructionSite's
  `sceneLoaded` callback (after Awake, before Start/first Update). It verifies
  ownership, the disabled capsule and the exact elevated pose, then pauses and
  resumes without releasing the arrival lease prematurely.
- Both directions are tested by simulating the real authored prefab and measuring
  particle world velocity. The vertical-first route, collision rejection, book
  unlock, pause/teardown and idempotent scene repair also pass.
- GPU appearance, touch usability and Android/iOS lifecycle remain manual checks.

## Changed files

- `Scripts/Gameplay/`: reusable controller, VFX adapter, target, ability, exit
  trigger and the rewritten `PlayerBeamInSequence` startup adapter.
- `Scripts/Game/GameplaySuspensionController.cs`: non-pausing transport lease.
- `Scripts/UI/BeamHoistButton.cs`: contextual touch control.
- `StarterAssetsInputs.cs`, `StarterAssets.inputactions`: HOIST intent/H binding.
- `ThirdPersonController.cs`: explicit motion reset at transport handoff.
- `Editor/GameplaySceneSetup.cs`, `Editor/Helpers/BeamTransportSetup.cs`: shared
  scene repair/content creation; `BeamTransportReview.cs` is opt-in review tooling.
- `Scenes/ConstructionSite.unity`: arrival wiring and supported spawn position.
- `Data/Progression/BeamHoist.asset`, `Data/Items/KnowledgeBooks/BeamHoistBook.asset`,
  `Prefabs/Items/KnowledgeBooks/BeamHoistBook_Dropped.prefab`,
  `Prefabs/BeamTransportVFX.prefab`: shared content using existing visuals.
- `Tests/Editor/Core/BeamTransportTests.cs`, `InGameMenuTests.cs`: focused contracts
  and actual-scene startup/pause regression coverage.

New assets include their Unity `.meta` files. Existing beam material files were
already present as untracked authored content when this task started.
