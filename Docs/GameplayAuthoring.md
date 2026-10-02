# Gameplay authoring V1

ConstructionSite uses reusable prefabs under `Assets/Game/Prefabs/Environment`.
Gameplay state remains with its existing owners; neither prefab owns combat, input,
camera or a second save system.

## Encounters

Drag `PF_AuthoredEncounter` into a gameplay scene. Select its BoxCollider and use
**Edit Collider** to fit the trigger. Move/rotate the inactive enemy children in
Scene view. The template contains three reusable melee enemies; edit the explicit
Enemies list when removing members, or use **Add dormant enemy member** with an
existing enemy prefab/archetype. Placement is the actual enemy Transform.

The encounter Inspector exposes each member's existing `EnemyEquipment` combat
profile and starting weapon. None means unarmed; choose an existing ranged-enabled
profile such as `AlienCombatV1` for a Plasma Pistol/Rifle. A melee-only profile
intentionally rejects guns. Equipment, scavenging and loot remain profile-owned.
Optional activation actions default to None. Completion remains derived from the
members' existing enemy/world state; no objective or reward is automatically added.

`Encounter01_FirstMelee` is now an instance of this prefab. Its working trigger,
two melee/unarmed members, authored poses, tuning and three stable world identities
are preserved. No encounter-specific combat or spawning code was added.

## ConstructionSite excavator repair

`_World/ExcavatorRepairMission` owns this level's requirements through
`ExcavatorRepairMission`. Its `ExcavatorArea_StartAndReturn` child is an instance of
`PF_GameplayTrigger`, configured Repeatable with the explicit Excavator Repair action.
Move/resize its BoxCollider to author the visit area. Its ground origin is
`(29, 0.0003, 37)`, with a `(6, 3, 4)` box centered one metre above it.

The three normal pickup prefab instances start inactive with stable scene identities:

| Pickup | World position | Placement |
| --- | --- | --- |
| Battery Cables | (-17.7322, 0.0402, -42.7832) | Electrical building front |
| Industrial Fuse | (-21.4900, 0.0408, -37.2229) | Electrical building west side |
| Hydraulic Fluid | (24.8762, 0.2400, -30.4627) | Office floor, clear of the existing table |

The first visit enables them and plays `CS_Excavator_RepairStart` through Amy's Girl
speaker variant. `CS_FindRepairParts` derives 0–3 distinct requirements from
`PlayerInventory`; extra copies do not add progress. The two electrical parts unlock
`CS_Excavator_OfficeClue` once if fluid is still missing. All three complete collection
and start `CS_ReturnToExcavator`. Returning rechecks possession, consumes one of each,
then calls the existing swing. Missing parts cannot start or charge for the finale.
No dialogue acquires input, camera or suspension ownership.

Flow: NotStarted → Collecting → ReturnToExcavator → RunningFinale → Completed.
The `excavator-repair` run participant owns only mission state and the clue flag;
normal pickups, inventory and objectives retain their existing save owners. Restore
waits for both world passes and Active Run readiness before reconciling peers. Removed
pickups stay removed. RunningFinale saves as Completed after payment, and reserves
`FinaleSavePoint` at `(30.5, 0.0537, 36)` outside the swept assembly. Saving does not
move the live player. Continue silently assigns the existing final machinery/rubble
poses, completes the return objective and retains consumed inventory, without replay.
Fresh attempts reload the original dormant content and blocked exit.

`ObjectiveController.SetProgress` supports absolute owner-derived counts. Optional
`CompleteObjective(..., showCompletion: true)` requests a transient presenter-only
checkmark for 2.5 scaled seconds. Restore and ordinary completions remain silent;
a new objective replaces the transient display immediately. The check uses two UI
strokes with the existing cyan color, avoiding font fallback/material changes.

Focused fixtures: `ExcavatorRepairTests` and `ExcavatorRepairPlayTests`. Device pickup
readability, dialogue pacing and native background/Continue remain manual checks.
No ambush, new encounter or generic mission framework is included.

Unity 6000.5.6f1: `ExcavatorRepair-02` passed **14/14** mission, generic-item,
communication and Active Run checks. `ExcavatorRepair-01` also passed all **9/9**
excavator/authoring checks; its lone failure was an older pickup fixture checking
deferred destruction before a frame advanced, fixed with `EditorTestFrame.Next` and
passed in the final batch. Coverage includes physical pickups, every collection save
stage, exact payment/extra-stack preservation, silent mid-finale Continue, normal
locomotion through the restored exit, solid rubble, and Hard Restart. The reviewed
completion capture is `Logs/ExcavatorRepair/completed.png`; XML/logs are under
`Logs/RepositoryAuditRemediation`. Existing scene records changed only for the new
mission parent link and completion display duration. `git diff --check` passed;
no full regression was run.

## Alien plasma bikes

The shared source is `D:\assets\Kids VS Aliens\stylized ray gun 3d model\Alien_PlasmaBike.blend`.
Only the production FBX is under `Assets/Game/Art/Vehicles/Alien`. It has 4,236 triangles
and four mesh materials. Cyan, violet and magenta variants share geometry and materials;
property blocks change emission, and two short trails provide the wake. There are no lights.
The bike-only URP shader uses fixed ambient/facet shading plus HDR emission so the navy
hull remains readable in this level's dark lighting; it does not receive realtime shadows.

`Assets/Game/Prefabs/Vehicles/PF_AlienBikeVisual` owns cosmetics only. Its two consumers
are `PF_AlienFlybyBike` (transform-driven, no colliders/AI/targeting) and
`PF_RideableAlienBike` (one Rigidbody and one primitive collision volume).

`AlienBikeVisualFeedback` lives only on the rideable wrapper. It observes controller
steering/forward speed and rotates BikeLeanPivot and its SteeringPivot. Defaults are
8 degrees bike lean, 10 degrees rider lean and 15 degrees control steering, with
exponential response rates 8/12, 15% lean at rest and full lean influence at 12 m/s.
These seven values are Inspector tuning; physics, markers and camera remain upright.
`PlayerBikeRider` requests the rider offset through its existing `PlayerAnimation`
authored-motion ownership. Only the CharacterVisual wrapper rotates, with cleanup on
transition, lease loss, disable, character replacement and release; nothing is saved.

The placeholder FBX has joined geometry. `Bike_RideHull` and `Bike_SteeringControl`
partition its existing triangles for the rideable instance, preserving the neutral
model/materials. The shared visual prefab, FBX and flyby prefabs keep their original
mesh. Future art replaces these cosmetic meshes/pivots without changing gameplay.

ConstructionSite's `AlienFlybys/Paths` contains ten cubic routes with named Start,
Control1, Control2 and End markers. Duplicate a route, edit those markers, then use its
Inspector **Validate route** button. Assign geometry/environment roots explicitly on
the controller. Validation conservatively includes renderer bounds as well as solid
colliders, a 2.25 m enclosing sphere and a continuous-curve sampling guard. It rejects
colliderless visual obstructions too. **Tools > Vehicles > Raise Selected Flyby Routes
To Clearance** is an explicit upward correction, never a runtime path search. Revalidate
after environment edits; build/play scene processing checks the current geometry again.
Changing a route's markers or clearance invalidates its bake. Runtime uses a compact
arc-length table and the exact cubic; no per-frame environment queries occur.

Seven routes have low passes (roughly 2.85–6 m world height, with clearance climbs up
to 10 m), two use medium 7–7.5 m passes, and one retains its 34–37 m high crossing.
Uniform route selection gives
a 70/20/10 low/medium/high mix; the high route is primarily audible ambience. The
low routes are checked from supported ground positions using the unchanged Action
gameplay camera. The three-instance pool chooses
13–23 m/s, 9–23 s event delays, reversible directions and three palettes. Single/pair/
three-bike events have 70/24/6 percent weights; same-route repeats are excluded when
alternatives exist. Pairs use the same cleared route with staggered starts. Trails remain
inside its already-cleared corridor. `Alien_Vehicle_Flyby` follows each moving bike in
3D, with 4/90 m min/max rolloff distances. Doppler remains disabled by AudioService.

For riding, drag the rideable prefab into a configured gameplay scene, use **Snap parked
bike to ground**, then author the ordered `mountApproaches` array, SeatPoint and both
dismount markers. Each array entry pairs an approach with a mount point; adjacent
approaches form the walking perimeter. The prefab has six approaches around the hull
and two shared left/right mount points; up to eight approaches are supported. The
3 m horizontal interaction radius is centered on the bike root, visible in its selected
gizmo. These are player-root poses, not bone sockets. Inspecting the instance assigns
its stable RunWorldObject identity. Canonical gameplay repair adds/fills PlayerBikeRider
and its replaceable rider animation controller without retuning existing values.
The temporary ConstructionSite instance is under `BikeRide_Test`, near (-32, 1.17, -35).
It has no objective, encounter or excavator-mission hooks.

`PlayerBikeRider` checks direct entries and both directions around that perimeter, then
chooses the shortest clear walk to a valid mount. Capsule sweeps, supported ground
samples and the seating arc reject obstructed or unsafe candidates. RIDE appears from
any side within the radius when a complete safe approach exists. The rider acquires
BikeTransition while Amy walks through her CharacterController to those markers;
each next segment is rechecked before walking it. PlayerAnimation observes that real
walking velocity, then uses the
authored Humanoid rider controller for the short hop/sit and seated phase. Replacing that
controller does not require editing bike physics. BikeRiding retains the on-foot lease
and allows only move/look/jump/run ingestion. Normal shooting, Beam, melee, inventory
activation and pickups stay unavailable; the capsule is disabled while seated. The
player root follows SeatPoint before the existing camera updates. Free look still uses
ThirdPersonController's camera-target method; camera presets/framing are unchanged.

Joystick vertical accelerates/brakes/reverses; horizontal steers. Jump hold charges and
release jumps; Run hold consumes turbo. The contextual RIDE/DISMOUNT button also accepts
desktop E. Dismount requires low speed, stable support and a clear side/capsule sweep.
Neither clear side means denial. Bike meters sit below the resource panel, away from
the dialogue region and touch controls.

| Starting tuning | Value |
| --- | --- |
| Acceleration / normal speed / reverse speed | 12 m/s² / 12 m/s / 4 m/s |
| Steering / steering fraction at normal max / lateral damping | 100°/s / .45 / 8 |
| Hover height / spring / damping | .85 m / 70 / 14 |
| Jump charge cap / min–max vertical launch speed | 1.4 s / 4–9 m/s |
| Turbo capacity / recharge / drain | 5 / .65 per second / 1 per second |
| Turbo acceleration / maximum speed | 21 m/s² / 20 m/s |
| Maximum dismount speed | 1.2 m/s |

Turbo recharges while mounted and grounded, including idle, and never while consumed.
Exhaustion requires releasing Run before boosting again. Jump cannot repeat in the air.
Pause/lifecycle blocking cancels charge/turbo and requires neutral input before reuse.
`AlienBikeAudio` requests Bike_Hover, Bike_JumpCharge, Bike_JumpRelease and Bike_Turbo
through AudioEmitter/AudioService. These registered SoundEvents reuse starter clips;
they are placeholders pending subjective listening and device mix acceptance.

The `alien-bike-v1` participant saves the latest stable bike transform with a validated
side exit and remaining turbo resource. While mounted or transitioning, the existing
Active Run safe-point reservation saves Amy at that matching exit. Continue assigns the
grounded bike absolutely in both world passes and restores Amy on foot, paused. It does
not replay mounting, resume a charge, preserve airborne motion or duplicate the bike.
Normal on-foot snapshots retain the existing player contract. Long airborne travel can
therefore return to the last safe grounded location.

Focused coverage belongs to `AlienBikeTests` and `AlienBikePlayTests`, alongside existing
input/suspension/audio/camera checks. Android/iOS touch feel, native interruption,
performance and subjective sound/readability remain device acceptance.

Unity 6000.5.6f1: `AlienBike-Final` passed **46/46** focused bike/input/suspension/audio/
persistence/camera checks. The final lease-loss and missing-environment safeguards plus
capture review passed **11/11** in `AlienBike-FinalSafety` (the bike subset, not 11 new
distinct checks). This includes real airborne Save/Continue returning paused, both
blocked dismount sides, disable during approach, repeated use, held throttle stopping
against the existing site fence, pooled flybys and repair preservation in both scenes.
Both runtime and Editor code compiled in Unity; no Full repository regression ran.
The final diagonal/high/close capture selection passed **1/1** in `AlienBike-CapturesFinal`.

The lower-flyby/radial-mount follow-up passed **13/13** in `AlienBikeFollowup-Final`
(`AlienBikeTests;AlienBikePlayTests` only). It revalidates all ten routes, observes all
seven common passes from supported ground in Action view at 23 m/s, clicks RIDE from
eight directions, checks left/right choice and blocked-side fallback, rejects unsafe
mount ground, and checks the walking capsule against the hull. Existing ride, jump,
turbo, collision, dismount and real Save/Continue checks remain passing. Unity compiled
the runtime and Editor changes; `git diff --check` passed. No Full regression ran.

Final steering presentation polish passed **16/16** in `AlienBikePolish-Final`
(`AlienBikeTests;AlienBikePlayTests;AlienBikeVisualFeedbackTests`). Unity compiled the
changes and verified directional/partial steering, smooth neutral return, rider/bike
agreement, low-speed response, turbo/jump, leaned dismount/lease-loss cleanup and
leaned Save/Continue. Neutral geometry/material equivalence and repeat-authoring tuning
preservation also passed. Action-camera neutral/left/right captures are in
`Logs/AlienBikePolish/review.html`. No Full regression ran. The polish diff is whitespace
clean; repository-wide checking flags 32 pre-existing lines in the byte-unchanged
ConstructionSite scene. Device touch feel/performance remain manual acceptance.

Original V1 captures remain in `Logs/AlienBike/review.html`; its elevated Tactical flyby
views are superseded by the follow-up ground-level Action views in
`Logs/AlienBikeFollowup/review.html`. The authored sit/hop is V1 art without exact
hand/foot IK, and the placeholder loop transitions/mix need listening on
speakers/headphones and devices.
Tests explicitly finish the scene's independent opening Beam before exercising bikes.
The earlier capture-only failures concerned review viewpoints, not route clearance or
ride behavior. XML/logs remain under `Logs/RepositoryAuditRemediation`.

## Dialogue and objectives

The scene's `GameplayCommunication` root has two independent owners:

- `DialoguePlayer`: one bounded speech queue and one semantic AudioEmitter. Lines
  run in order on scaled time, with optional gaps. It never acquires suspension or
  controls movement, combat or the camera. Disable/scene exit stops voice and clears
  transient dialogue; Continue does not replay it.
- `ObjectiveController`: one active tracker plus retained inactive/completed/progress
  records. Count objectives complete at their target. Starting another objective
  deactivates the previous tracker; completed records remain available to conditions.

CharacterVisual references a `DialogueSpeaker` asset containing display name,
Boy/Girl voice type and optional stable special-character ID. Message content lives
in `DialogueMessage`, created through **Create > Gameplay > Dialogue Message**.
Exact special-ID matches win; otherwise the speaker's Boy/Girl variant is used.
Each variant owns sequential text, optional SoundEvent, duration and following gap.
Keep individual subtitle lines short; split longer speech into multiple lines.

Audio None works without further setup. Later voice files belong to Voice-category,
2D SoundEvents registered in AudioLibrary, referenced directly by the resolved line.
Duration zero uses actual voice length/pitch plus padding, or reading time when
silent; positive duration overrides it. Voice uses existing AudioService policies.
No trigger plays raw clips, and no TTS or source animation is involved.

Put content in `Assets/Game/Dialogue/Shared` or `ConstructionSite`; name messages
`<LEVEL>_<WHEN/OBJECT>_<PURPOSE>`. The Inspector reference opens the exact text/audio
asset. `CS_Intro_FindWayOut` is an editable sample with no voice, not an extra live
trigger. `CS_Intro_WhereAmI` supplies Amy's silent Girl line, **Where am I?**, for
three seconds after ConstructionSite's fresh LevelStart arrival releases control.
`PlayerBeamInSequence` observes its existing destination/end events and calls the
dialogue owner once. Cancelled descent and Continue do not replay it; a new attempt
or Hard Restart allows it again. It does not change the opening objective or the
Healing Pod event. Amy and Granny have explicit speaker assets on their visual prefabs.

Create objectives through **Create > Gameplay > Objective** under
`Assets/Game/Data/Objectives`. Configure ID, title, None/Count and target count.
The author-facing ID describes the objective; existing catalog GUIDs resolve its
asset in saves. `CS_FindWayOut` is the real ConstructionSite opening objective.
`ActiveRunController.Start` initializes it only in the fresh-attempt branch, after
starting loadout setup. Continue restores state and skips this initializer.

## Triggers and direct calls

Drag `PF_GameplayTrigger`, move/resize its nonblocking BoxCollider, then configure:

- OneShot or Repeatable (once per visit, not every physics frame).
- Optional required objective/state and Knowledge.
- Optional Dialogue, Objective Start/Update/Complete and existing system message.
- Optional `AuthoredBeamArrival` reference for a preauthored world arrival.
- Remember One Shot for Active Run persistence.

All None/empty combinations are valid. Player root entry is required, and activation
waits for Active Run readiness and rejects suspended/dead players. Conditions may
become satisfied while inside. Repeatable visits rearm after leaving the volume.
`GameplayActions.Execute(player)` is the same small Inspector block used by encounter
activation; gameplay owners can also call `DialoguePlayer.Play`,
`ObjectiveController.StartObjective/AddProgress/CompleteObjective` directly.

Scene identities are assigned when inspecting these prefab instances and by
**Tools > Setup > Setup or Repair Active Gameplay Scene**. Prefab assets retain blank
scene identities. Run canonical repair after adding content to validate IDs and
register new objective assets in RunContentCatalog. Repair fills missing communication
dependencies/references while preserving authored values. Ambiguous duplicate owners
fail preflight rather than silently selecting one. The completed one-off migration
recipe was removed; routine repair does not regenerate Encounter01 or content assets.

## Presentation and persistence

`GameplayCommunicationView` presents objective and CC independently.
`GameplayMessageLayout` only reserves regions inside the existing SafeArea: objective
at top-center, existing run/system feedback below it, dialogue above quick slots.
Existing feedback scheduling and message behavior are unchanged. New panels ignore
raycasts. The objective is a compact navy/cyan pill with a reticle and separate cyan
count. CC is a shallow inline speaker/divider/transcript strip with a subdued
cyan-to-magenta edge. Both reuse `NeonPanel` and its shared material. TMP uses the
project's SIL-OFL Liberation Sans SDF with shared `DialogueText` outline material,
bold speaker/count and regular transcript.

Maximum normalized SafeArea regions (bottom-left origin): objective
`(0.36,0.86)-(0.68,0.956)`, run toast `(0.30,0.79)-(0.71,0.845)`, gameplay feedback
`(0.28,0.715)-(0.73,0.775)`, CC `(0.25,0.185)-(0.75,0.35)`. Objective panels anchor
at the top center of their region; CC anchors at its bottom center above quick slots.
The view uses actual TMP preferred text widths plus padding, icon/name, divider and
optional count. Inspector width ranges are 190?560 objective / 220?860 CC canvas units,
also capped by the SafeArea region. Long text wraps and grows in height; exceptional
content uses the existing TMP font-size fallback within that region. Short messages
stay shallow. Reflow happens on content/screen changes, not every playback frame.
These owners remain separate; layout does not queue, merge or change messages.

The `objectives` participant persists objective states/progress on the separate
communication root. `gameplay-trigger` saves remembered one-shot use on its own
stable world object. Both restore absolutely on either world pass without actions,
voice or rewards. Enemy persistence is unchanged. Fresh runs/Hard Restart reload
authored unused triggers and activate the configured opening objective.

## Authored Healing Pod arrival

`PF_HealingPodBeamIn` composes `PF_GameplayTrigger`, `AuthoredBeamArrival`, an inactive
Payload Gate containing the original `PF_HealingPod`, and the existing
`PF_BeamTransportVFX`. ConstructionSite's `HealingPod_BeamIn` is the live instance.
The arrival owner never controls Amy, leases input, grants health or duplicates pod
capacity. It drives only presentation, placement and the collision/interaction gate.

The hierarchy is one root GameplayTrigger/AuthoredBeamArrival/RunWorldObject and
kinematic Rigidbody, `TriggerVolumes` with dumb child BoxColliders, `ArrivalBeam`, and
`Payload Gate/HealingPod`. The inherited single root collider is disabled in this
variant; ordinary PF_GameplayTrigger still uses its single collider. Child boxes have
no custom scripts, IDs or lists. The root discovers only descendants of the explicit
TriggerVolumes reference. All enabled boxes are one union for entry and landing.
Crossing overlaps is one visit; leaving every box rearms repeat/aborted attempts.
A cheap membership check also handles missing physics exits during capsule disable.
Rejected arrival attempts do not repeat placement checks while Amy remains inside.

Drag the prefab, move/yaw-rotate its root, then move/rotate/resize/add/delete child
BoxColliders. Editor changes regenerate a deterministic grid after editing settles
(and before interactive Play), validate against actual ground/clearance, and store
accepted root-local poses. No manual candidate registration is needed. The Inspector
shows validation state/count and **Regenerate / Validate Candidates**; selected gizmos
show every box and accepted candidate. Resize boxes rather than scaling the event root.
Nearby level-geometry edits still require the explicit validation button. Prefab-stage
editing does not bake against unrelated open-scene geometry; validation runs on the
placed scene instance. Moving the root carries all local records naturally while the
Editor rechecks its new world support. Runtime never generates candidates.

The validator samples 25 ground points per candidate, checks ground variation/slope,
the complete allowed footprint, and collider/overhead clearance. Runtime rejects
candidates inside `Minimum Player Distance`, then prefers those comfortably inside
the current gameplay camera with a clear sight line to the pod. Among those, the
nearest safe distance keeps the arrival readable. If none are visible, it chooses
the nearest safe candidate; equal distances retain authored order. Camera projection
and up to three bounded sight rays per candidate never move the camera or search for
new ground positions. The effective minimum also includes current max move/sprint speed times
Beam Arrival Duration, footprint radius, player capsule radius and safety margin.
Pending selection reevaluates every 0.15 s without allocations or ground searches.
The Beam preview travels at up to 12 m/s between accepted candidates. A candidate
must remain safe and reached for 0.4 s before materialization starts. These values
are Inspector-authored. No valid candidate means the event remains pending; player
movement/camera visibility can change the selection before lock. Once the pod starts
materializing, its pose freezes and selection stops.

ConstructionSite's region was measured from terrain, fences, excavator and
container/ramp geometry. Its main box has X/Z bounds `[-39.7,-20.7] / [-11,-4.4]`;
it extends below ground so a grounded CharacterController root is inside. The accepted
1 m grid currently yields six candidates in the clear left/right ground clusters.
The center hill/container slope fails footprint support checks and is excluded.

All preserve the pod's authored facing. Minimum distance is 7.8 m; arrival is 1 s;
footprint radius is 2 m, clearance height 3.5 m and safety margin 0.75 m. Optional
Dialogue, Objective Action, Objective and System Message are empty. The existing pod's
stable ID, scene-specific door/heal timings and all gameplay configuration are retained.

The Beam preview fades in while the payload stays completely inactive. After the
stable target locks, the pod grows/settles into place. Every payload collider and
explicitly assigned interaction behaviour stays disabled throughout materialization.
If Amy enters the locked footprint, progress waits without moving either Amy or the
landing pose. A dynamic blocker also delays completion; a bounded nonallocating box
check retries at the same 0.15 s interval. Once clear, original collision/interaction
resumes and the Beam fades out. Existing Healing Pod logic then owns proximity/healing.
Death or disable rolls unfinished arrival back to available without acquiring control.
Aborted visits retry after leaving/reentering the full compound region.

The `beam-arrival` participant saves only committed arrival and candidate index.
The pose lock is presentation-only, not a durable successful-arrival commit.
Pending or materializing saves record an unused trigger/event, including when Amy
has entered the disabled footprint. This avoids restoring an interactive pod on top
of her. Only safely completed arrival is persisted. Both restore passes keep the payload
gate hidden; after Active Run readiness, a committed payload becomes active at its own
restored world pose, without Beam/audio/actions replay. The pod's existing participant
alone restores healing capacity. Continue remains paused; fresh attempts reset the event.
No extra pod is instantiated. Beam presentation retains its existing one-time strand/
buffer initialization and shared materials; no per-frame managed allocations, runtime
material copies or new lights are introduced by the arrival owner.

The safe fallback can still lie outside the ordinary gameplay camera when no validated
safe point fits the view. The current narrow strip has two separated safe ground
clusters: at its extreme approach entries, nearby visible candidates are too close,
while distant safe candidates are offscreen. Adaptive selection cannot create safe
ground or expand the camera frustum. The region and camera remain unchanged as requested;
actual-camera captures retain this limitation rather than claiming visible arrival
from both extremes. Preview selection remains adaptive until materialization locks.

## Validation

Unity 6000.5.6f1: `ConstructionCompound-Final` passed **130/130** focused/adjacent
checks: arrival selection/lifecycle, compound and existing single-box triggers,
Encounter01 (4/4), UI/dialogue/objectives, Healing Pod, Beam traversal/presentation,
Active Run, LevelStart, suspension, feedback and repair. The earlier focused pass
`ConstructionCompound-Adjacent` passed 30/30; adaptive lifecycle/UI iteration passed
7/7 in `ConstructionCompound-Adaptive`. No Full regression was claimed: the final
selection explicitly covers the changed shared trigger/presenter and neighboring owners.
The final Editor-only safeguards then passed **4/4** in
`ConstructionCompound-EditorFinal`, including rejecting validation in prefab preview scenes.

Coverage includes one/L/overlapping boxes, root movement/rotation, child edits and
addition/deletion, deterministic rebakes, one-shot/union visits, pending retargeting,
stability/locked intrusion delay, disable and actual Save/Continue on both sides of
completion. The warmed candidate selector allocated **0 managed bytes over 200 calls**;
this is a focused measurement, not device profiling. Encounter assertions now match
the scene's two authored enemies; the generic three-member template is unchanged.

The first two iterations exposed a new-box validation ordering bug and missing
CharacterController exit callbacks, both corrected before the final pass. The former
instant-arrival tests were updated for the requested pending/locked lifecycle. The
camera captures exposed the documented region/safety limitation; the approach checks
verify preference and deterministic fallback, not a false guarantee of on-camera
arrival from both extremes.

URP captures include all four requested texts plus long wrapping examples at 1600x720,
1280x720 and 1024x768, with all channels visible and simulated SafeArea insets. Open
`Logs/ConstructionCompound/review.html`; actual approach captures are retained there
alongside the UI images. XML/logs are under `Logs/RepositoryAuditRemediation`.
The Healing Pod prefab is byte-identical. Against the pre-correction scene snapshot,
only the arrival instance configuration and communication view width fields changed;
geometry, encounters, Aurora and the existing pod tuning were preserved.
`git diff --check` passes. Device touch/readability, camera-mode framing, preview motion
feel, GPU cost and Android/iOS interruption remain manual checks.

Original Gameplay Authoring V1 evidence:

Focused fixtures: `GameplayAuthoringTests`, `GameplayCommunicationPlayTests`, plus
existing `AuthoredEncounterTests`/`AuthoredEncounterPlayTests`. These exercise real
Save/Continue, hard restart, conditions, resolution, semantic voice, nonblocking CC,
repeatable entry, repair and simultaneous channels at 1600x720, 1280x720 and 1024x768
with simulated safe insets. Device subtitle readability, touch ergonomics, final
voice listening and native lifecycle behavior still require manual checks.

Unity 6000.5.6f1: `GameplayAuthoring-02` passed **12/12** focused checks.
`GameplayAuthoring-Full` passed **396/397**, including all new/adjacent authoring,
encounter, persistence, Healing Pod, Inventory, LevelStart, repair and audio checks.
The only failure was the existing isolated ranged-combat fixture's three-second
reload/fire deadline (`AlienCombatantTests.LowCoverReloadAndWorldState`). It passed
unchanged in `GameplayAuthoring-ReloadCheck` (**1/1**); the full run is therefore
recorded with a timing-sensitive failure, not as an entirely green run. Combat code
and tuning were not changed. Initial new-fixture failures involved natural trigger
activation preceding an explicit test call and querying a cleared singleton after
disable; both test fixtures were corrected before the focused pass.

XML/logs are under `Logs/RepositoryAuditRemediation`; reviewed UI captures are in
`Logs/GameplayAuthoringV1/communication-*.png`. Scene review preserved 2,487 existing
records verbatim; changes were limited to the encounter prefab connection and new
communication wiring. `git diff --check` passed. No device deployment was performed.
