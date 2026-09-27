# Healing Pod gameplay V1

`Assets/Game/Prefabs/Environment/PF_HealingPod.prefab` owns the mechanic through its
root `HealingPodController` and `RunWorldObject`. `HealingPodTrigger` forwards
explicit proximity/chamber volumes. `PlayerCharacter` identifies the player.
Proximity opens doors at any charge level; only the player's root inside the
chamber can request healing. Normal movement/combat stays owned by the existing
suspension system.

## Authored tuning

Prefab values and scene-instance overrides are both authoritative. The final-polish
pass preserves the existing values, markers, moving colliders and ConstructionSite
file. Do not run a generator or reset these values from C# defaults.

| Phase | Base prefab | ConstructionSite instance |
| --- | --- | --- |
| Align and face PlayerAlignPoint | 0.45 s | 0.45 s |
| Close doors and rear-hinged lid | 0.55 s | 1 s |
| Rise to PlayerFloatPoint | 0.65 s | 0.65 s |
| Hold, then grant healing | 0.85 s | 1.5 s |
| Lower | 0.55 s | 0.55 s |
| Reopen | 0.55 s | 1 s |
| Walk to PlayerExitPoint | 0.8 s | 0.8 s |

Totals are 4.4 s / 5.95 s plus frame rounding and any final ground contact.
Smoothstep absolute poses preserve reversible door motion without drift. Float
height remains 0.12 m. The indicator stays parented to the lid.
ConstructionSite retains position (-23.69, 0.176327, -21.39) and the user's current
yaw -174.51 degrees, near LevelStart.

## Healing capacity and feedback

`healingCapacity` is serialized in full-health-bar units: 1 supplies 100 percentage
points of whichever character's maximum health is current. `RemainingCapacity`
belongs to the pod; `PlayerHealth.Heal` remains the authoritative health writer.
The grant is clamped to missing health and supply. Only the actual restored
fraction is charged. Armor is never changed and dead players cannot heal.

| Player health before | Charge before | Health after | Charge after |
| --- | --- | --- | --- |
| 40% | 100% | 100% | 40% |
| 60% | 40% | 100% | 0% |
| 20% | 40% | 60% | 0% |
| 100% | Any available charge | 100% | Unchanged |

Full health rejects the sequence before control is claimed and reports
`HealthAlreadyFull` through `PlayerFeedback` / `GameplayFeedbackPresenter`:
**HEALTH ALREADY FULL**. Existing semantic denial audio accompanies the feedback.
A visit suppresses repeated chamber feedback/sounds until the player leaves
proximity. Completing a successful walk-out also allows one fresh chamber attempt
to report its denial. Approaching alone never plays the depleted denial.

Zero charge means `IsDepleted`. Chamber/indicator renderers are off, but proximity
still opens and closes the pod. An empty chamber attempt cannot claim control,
heal, or consume charge. Partial charge leaves the pod available for another use.

## Control, falling presentation and walk-out

The pod acquires its existing `HealingPod` input/combat suspension lease and an
explicit authored-motion reservation on `PlayerAnimation`. The latter owns
Animator writes and cleanup; gameplay code contains no clip or Animator-state
names. No new player component or movement framework is required.

During rise/hold/lower, `PlayerAnimation` activates the existing
`FloatingPresentation` layer used for genuine long falls, with its existing
`Floating.fbx` clip. The configured speed multiplier is 0.45 of the prior Animator
speed. Opening restores that speed and clears the layer. No clip, import setting
or Animator Controller is modified. Completion, abort, disable, death and visual
replacement release presentation ownership and restore the captured speed.

Align/rise/lower retain capsule-validated smooth root motion. Walk-out enables
the existing CharacterController, moves toward the prefab exit with physical
collision/gravity and drives the normal walking blend from actual capsule
velocity through PlayerAnimation. Input remains suspended until the exit is
reached and the capsule has ground contact. A blocked exit aborts instead of
leaving the player permanently leased. Abort opens the physical exit and uses
the validated safe endpoint when clear; normal successful exit does not teleport.

## Active Run

The `healing-pod` participant saves remaining capacity under the stable world ID.
A small fallback reads original V1 boolean-only snapshots as full/empty. Current
capacity restoration is absolute and idempotent; it grants no health, spends no
charge and replays no audio/VFX. Doors restore closed, then follow proximity.

Before health notifications, the pod accounts for the exact clamped grant and
then requests a save. During the interaction Active Run saves PlayerExitPoint
position/facing with zero vertical velocity without moving the live player.
Continue before commit restores prior health/charge; after commit it restores
saved health/reduced charge. It never resumes a floating sequence and retains
the existing paused Continue flow. Completion/abort clears only this reservation.

Scene repair supplies authored stable IDs. Later spawns use the existing catalog
and `RunWorldObject.TrackSpawn` contract. Activation need not occur at scene load.
No Beam-in spawning or Beam transport coupling is implemented.

## Visual swap and collision

Keep root logic, persistence, `PlayerMarkers`, `InteractionTrigger`,
`ChamberTrigger`, static `Collision` and `HealVfxPoint` when replacing `Visual`.
Reassign explicit door/roof/powered-renderer references and fit/reattach the two
prefab-owned DoorCollision children. Each is a BoxCollider with a kinematic body
following its door transform. Nothing rebuilds/cooks collider geometry at runtime.
No MeshCollider or blocking trigger is used.

The proxy is authored open; prefab overrides supply closed poses. Do not reset
the imported root's axis conversion. Shared FBX meshes, meter scale, door pivots
and the rear hinge remain unchanged. Shared window materials preserve visibility
inside the closed proxy; no runtime material copies are created.

| Setting | Value |
| --- | --- |
| Proximity radius / center height | 3 m / 1 m |
| Chamber size / center | (0.8, 1.5, 0.8) / (0, 0.8, 0) |
| Left / right closed local position | (0.49, -0.72, 0.29) / (-0.49, -0.72, 0.29) |
| Left / right open offset | (0.55, -0.08, 0) / (-0.55, -0.08, 0) |
| Roof opening | -75 degrees around local X |
| Align / float / exit local positions | (0, 0.36, 0) / (0, 0.48, 0) / (0, 0.36, 2.15) |

## Energy and audio

HealVfxPoint retains 24 motes/second with 0.8-second lifetime, about 19 concurrent
and a cap of 32. Its new HealingEnergy child reuses the existing `BeamEnergyField`
presentation component and `Game/Beam Energy` shader with green/cyan shared
materials and one narrow rotating spiral (161 vertices / approximately 320
triangles). One strand avoids runtime strand duplication. The particle buffer
is allocated once and reused; no realtime lights or material copies.
Energy runs only during rise/hold/lower and stops on cleanup.

`HealPod_Open`, `HealPod_Healing` and `HealPod_Depleted` retain their existing
SoundEvent / AudioService / AudioEmitter wiring and replaceable placeholder clips.
Temporary authoring helpers are removed after use.

## Validation

Focused fixtures cover capacity examples, different maximum health, full-health
toast/no claim, depleted doors and chamber denial, sound suppression, physical
walk-out, slow falling presentation/restoration, abort/death/disable, actual
Save/Continue before/after commit, late activation, idempotent restore, authored
tuning preservation and primitive door collision/reversal.
The Play Mode fixture captures actual URP healing and walking views under
`Logs/HealingPodV1/healing.png` and `walking.png` in the isolated test project.

Unity 6000.5.6f1: `HealingPodPolish-Full` passed **365/365**, including the
adjacent falling/Beam presentation, suspension, feedback, audio and Active Run
checks. The final pod-only fresh-attempt feedback adjustment was then covered by
`HealingPodPolish-Final`: **12/12** focused tests passed. No failures or skips in
either run. XML/logs are under `Logs/RepositoryAuditRemediation`.
The pre-polish comparison confirmed all original prefab motion/timing values and
14 collider/Rigidbody blocks unchanged, with ConstructionSite byte-identical.
`git diff --check` passed. Captures are also retained in `Logs/HealingPodPolish`.

Manual/device checks remain: touch approach/exit, subjective animation cadence
and audio balance, GPU overdraw/frame rate, Android background/force-stop/Continue,
and iOS lifecycle behavior. Editor evidence does not establish device performance.
