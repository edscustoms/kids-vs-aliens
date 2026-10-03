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

For a mission/chest-owned encounter, disable **Activate On Entry** and its entry
collider. The owner calls `Activate()` once; distant guards remain at their authored
positions until normal perception detects Amy. Trigger entry retains its existing
investigate-player behavior. Restoration assigns the encounter flag only; each
enemy restores its own active/dead state, so neither world pass reactivates members.

`Encounter01_FirstMelee` is now an instance of this prefab. Its working trigger,
two melee/unarmed members, authored poses, tuning and three stable world identities
are preserved. No encounter-specific combat or spawning code was added.

## ConstructionSite excavator repair

`_World/ExcavatorRepairMission` owns this level's requirements through
`ExcavatorRepairMission`. Its `ExcavatorArea_StartAndReturn` child is an instance of
`PF_GameplayTrigger`, configured Repeatable with its Action Target assigned to the mission.
Move/resize its BoxCollider to author the visit area. Its collider center is
intentionally offset from the root: approximately `(22.11, 1, 46.18)` in world space,
with size `(6.84, 3, 7.03)`. Use the actual volume when checking entry/return.

The three normal pickup prefab instances start inactive with stable scene identities:

| Pickup | World position | Placement |
| --- | --- | --- |
| Battery Cables | (-17.7322, 0.0402, -42.7832) | Electrical building front |
| Industrial Fuse | (-21.4900, 0.0408, -37.2229) | Electrical building west side |
| Hydraulic Fluid | (24.8762, 0.2400, -30.4627) | Office floor, clear of the existing table |

The first visit after acquiring the crash pistol enables them and plays `CS_Excavator_RepairStart` through Amy's Girl
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
The current combat authoring below extends the original repair content.

### ConstructionSite combat pass

`_World/ConstructionSite_Combat` contains the additional authored encounters.
The original `Encounter01_FirstMelee` and its two melee members are unchanged.

| Encounter | Activation owner / condition | Members |
| --- | --- | --- |
| CS_Repair_Route4 | Repair enters Collecting | 2 Pistol |
| CS_Repair_Electrical | Repair enters Collecting | 2 Pistol |
| CS_RifleChest_Ambush | Route 5 chest finishes opening, after loot appears | 2 Pistol + 1 Rifle |
| CS_Excavator_Return | Repair enters ReturnToExcavator with all three parts | 3 Pistol + 2 Rifle |

Repair holds explicit references to its three encounters and requires possession
of the crash-site pistol before starting. The chest uses `GameplayActions.encounter`
from `LootChest.onOpened`; restored open state never replays the action. No new
kill objective or proximity activation is involved in these four fights.

`CS_Crash_FirstPistol` supplies the first gun with its normal full 12-round magazine.
The old three loose rifle placements were removed so `CS_Route5_RifleChest` supplies
the first rifle. Its two unique loot prefabs and min/max count of two guarantee one
Rifle and one six-Plasma pickup. Pistol Handling is beside the crash pickup; Rifle
Handling is immediately before the chest. Opening requires the pistol and learned
Rifle Handling, preserving the first-gun order and immediate rifle usability. A
rejected proximity hold resets for another approach after the requirements are met.
Canonical scene repair ensures the existing `ProximityInteractor` marker on the
player root; ordinary chest proximity holds require it.

Guaranteed Plasma: crash pickup **9**, electrical approach **12**, Route 5 chest
approach **18**, chest **6**. At zero random drops, the first four enemies budget
40 pistol shots (12 loaded + three reloads = 48, costing 9 Plasma). The next eight
budget 192 rifle rounds (28 loaded + six reloads = 196, costing 36 Plasma). This
allows 20% pistol misses and 25% rifle misses with 8/4 rounds left respectively.
Enemy drops remain the existing 75% chance of 2–4; they are additional margin,
not a prerequisite. Weapon stats, reload costs and enemy HP are unchanged.

`ConstructionCombatTests` checks content, loadouts, clearance, navigation and wiring.
`ConstructionCombatPlayTests` exercises physical chest opening, normal pickup/book
use, inventory reload payment with that miss budget, Save/Continue at activation
boundaries, partial deaths, completed repair and Hard Restart. Budget damage is
applied directly in that test; it does not certify human aim or difficulty. Review
captures are under `Logs/ConstructionCombat` in the isolated test project.

Unity 6000.5.6f1: `ConstructionCombat-Final` passed **16/16 focused tests**, including
the original melee encounter, repair lifecycle and interaction repair on disposable
GamePoc/ConstructionSite scenes. No full regression was run. Reviewed captures are
also copied to the working project's `Logs/ConstructionCombat/CS_*.png`; results are
in `Logs/RepositoryAuditRemediation/ConstructionCombat-Final-tests.xml`. Human/device
combat feel and touch aiming remain manual acceptance checks.

Original repair-pass evidence: `ExcavatorRepair-02` passed **14/14** mission, generic-item,
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

The intentional right perimeter fence required lifting Path_07's authored route root
by **1.9 m** (controls retain their local positions). Its lowest pass is now about
**4.75 m**, still in the common low category. All ten routes retain the full 2.25 m
clearance radius; no fence or other route was changed.

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
player root follows SeatPoint before the existing camera updates. Mounted look still
uses ThirdPersonController's existing input and camera-target method.

`GameplayCameraController` owns temporary bike framing on the existing Cinemachine
rig. It finds the rider through the authored Follow target's player ancestor; normal
scene repair already supplies this relationship. `PlayerBikeRider` owns the phases,
and exposes only its transition duration to the camera. Neither bike physics nor the
saved Action/Tactical/Isometric preference owns or persists camera blend state.

Tune `GameplayCameraProfile.asset > Bike`: distance **4.8 m**, root height **2.5 m**,
additional downward pitch **4 degrees**, FOV **70**, look height **0.7 m above SeatPoint**,
forward look-ahead **2.8 m**, and follow damping **0.12 s**. Position/yaw use the stable
bike root and seat, independently of cosmetic lean. Input-authored look angles exclude
rotations inherited from walking/mounting; existing desktop look remains available.

The **0.8 s** mount blend starts only after accepted RIDE, during Approaching. A short
approach caps its remaining blend to the rider's seating phase. The **0.6 s** return
starts at Dismounting and is capped by that phase so foot framing returns with control.
`Transition Yaw Speed` caps mount/dismount yaw at **110 degrees/s** independently of
distance, height and lens. Position and view share that capped heading around Amy's
live presentation anchor, including while mounting and dismounting. The offsets blend
in heading space during transitions. Small turns settle within the normal blend;
large turns retain their yaw tail through Riding or OnFoot. Riding resumes the original
world-space position/rotation damping, allowing acceleration, stopping and turning to
shift the composition. A remaining mount yaw tail rotates that dynamic pose without
pinning the rider. Each handoff preserves the incoming pose; normal OnFoot releases
the orbit to its preset. The transient yaw is not saved.
The normal preset continues to drive the underlying rig throughout. Bike presentation
blends its position, orientation and lens after the existing pipeline stages, leaving
noise, render feedback and occlusion with their existing owners. Isometric uses a
temporary projection-matrix blend; it releases that matrix at either endpoint and on
disable. Abort recovery uses unscaled time when the rider is back on foot, including
death/pause cleanup. Continue restores the usual saved on-foot preset, paused.
The existing occlusion owner uses its player-relative sample fallback while Amy's
seated capsule is disabled (disabled collider bounds are empty). While mounted, the
ten context samples protect the hull between root and seat; the five body samples
still protect Amy. The low-prop cutoff uses the bike root. On-foot sampling, thresholds,
fade rules and authoritative colliders are unchanged. ConstructionSite's two pipe
stacks have CameraOcclusionGroup and live under LevelGeometry for its startup cache;
reparenting preserves all world poses. No runtime cache rebuild or obstruction owner
was added.

`BikeCameraTests` covers all three preset round trips, interrupted mounts, disabled
bike/rider, lease loss, death, paused Continue, stable yaw/look and cosmetic lean,
plus foreground container/pipe fades. It reuses the existing ride/jump/turbo/collision
and airborne Continue flows. Following the Path_07 repair, these tests use normal scene
build processing with all flybys present. Completion guards still reject aborted
Play Mode setup as a false pass.

Unity 6000.5.6f1: `BikeCamera-Acceptance` passed **18/18** focused checks
(`BikeCameraTests;MenuCameraTests;CameraOcclusionSilhouetteTests`), with graphics and
runtime/Editor compilation. The five transition stages and close-obstruction views
are in `Logs/BikeCamera/review.html`. `git diff --check` passed. No Full regression
ran; Android/iOS touch feel, lifecycle and device rendering remain manual acceptance.

Joystick vertical accelerates/brakes/reverses; horizontal steers. Jump hold charges and
release jumps; Run hold consumes turbo. The contextual RIDE/DISMOUNT button also accepts
desktop E. Dismount requires low speed, stable support and a clear side/capsule sweep.
Neither clear side means denial. Bike meters sit below the resource panel, away from
the dialogue region and touch controls.

Mounted combat retains Amy as the target. `PlayerBikeRider.OccupiedBike` and its torso,
upper-body, side and seat samples replace the disabled CharacterController's empty
bounds for enemy perception/aiming. The occupied hull counts as that target in LOS;
world cover and the nearest physical shot collision remain authoritative. Melee
approaches the hull perimeter with the same authored surface reach. `AlienBikeImpact`
on the rideable wrapper implements IDamageable only to forward enemy-originated hits
on an occupied hull to the rider's existing PlayerHealth. Empty bikes take no damage.

`AlienBikeImpact` also owns physical collision-to-enemy rams, deduplicated by EnemyActor.
Below 4.5 m/s there is no damage/launch. Otherwise `t = Clamp01((speed - 4.5) / 15.5)`;
damage is `Lerp(20, 140, t)`. Inspector fields expose both speed thresholds, both damage
endpoints, horizontal/upward knockback and repeat cooldown (0.65 s). The default
9 m/s horizontal and 3.8 m/s upward impulses scale from 55%/65% to 100% with `t`.
Only a player-driven rideable wrapper dispatches rams; shared art and flybys remain
non-combat. Canonical scene repair fills a missing impact component without retuning.

`EnemyMotor` owns the brief external-impact movement lock and swept ballistic motion,
temporarily disabling its NavMeshAgent. Capsule sweeps stop world obstruction and
connected NavMesh probes retain a safe ground endpoint. Living enemies settle there
and resume navigation; lethal hits still use EnemyHealth/EnemyDeathSequence while the
launch remains visible. Damage goes through CombatHitResolver, including normal hit
reaction, health UI, loot/death and run removal. RunWorldObject captures the motor's
ground endpoint during flight; restore clears the transient displacement before
assigning state. Neither impulse nor an airborne pose is persisted.

Four-fix validation (Unity 6000.5.6f1): `BikeFourFixes-Acceptance` passed **35/36**;
the remaining airborne Save/Continue fixture selected arbitrary actors and placed
victims together. It now selects 140-HP authored actors on separate clear native
ground. `BikeFourFixes-RamFinal` passed **3/3**, including that real reload, physical
Rigidbody impacts, low/normal/turbo damage, multi-collider deduplication, living/dead
launches, navigation recovery, wall sweeps and ground edges. Together these cover
**37 distinct passing focused checks**, including small/180-degree yaw, all three
camera presets, existing ride/jump/turbo/dismount, occlusion, on-foot combat/cover,
mounted ranged/melee damage, empty-bike exclusion, all routes/build processing and
repeat repair preservation. The camera fixture no longer bypasses flyby validation.
XML/logs are under `Logs/RepositoryAuditRemediation/BikeFourFixes-*`; launch captures
are in `Logs/BikeFourFixes`. `git diff --check` passes. No full regression or device
run was performed; subjective camera/impact feel and Android/iOS acceptance remain
manual checks.

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

## BikeRoute graybox

Open `Assets/Game/Scenes/BikeRoute.unity`, enter Play Mode, use RIDE and follow the
route to the green finish stripe. V3 replaces the rejected flat environment with
close terrain cuts, forest clusters and service buildings. **Manual driving acceptance
is pending.** Existing bike/camera, HUD/input and Active Run owners are unchanged;
there is no combat, mission, pickup or ConstructionSite transition.

The eight sections retain approximately **5.23 km** of primary progression, or
**4.64 km** using both shortcuts. Asphalt-only sections 01/03/05/08 alternate with
mandatory dirt 04/07 and road/dirt choices 02/06. A Direct Wash is **613 m** versus
02's **952 m**; B Underpass is **692 m** versus 06's **944 m**. Both rejoin forward.
Asphalt widths remain **5.5-11 m**, dirt **6-8 m**. Road elevations now span roughly
**6-58 m**: an initial climb, high sweeper, descending narrow S, wash climb/descent,
wide turbo bowl, elevated crossing, rolling ridge and downhill finish. The lower
shortcut runs beneath the supported upper road; there is no mandatory bridge gap.

Most corridor sides are **5.5-9 m from the centerline**; 05 opens to **17.5 m**.
Steep banks and matching static cut-face colliders enforce the sides, with deliberate
openings at forks/rejoins. Local retaining ends close the start pocket and finish
run-out. The bridge has a full-width shoulder deck beneath its asphalt; only this exposed
span uses invisible safety planes above its low parapets.
These are local boundaries, not a world-sized enclosure. Collision probes cover
normal riding and 7.2 m above the road; they do not prove every airborne escape impossible.

`LevelGeometry` contains `Corridor Boundaries`, **70 Forest Clusters / 339 trees**
from Conifers [BOTD] URP, **9 Road Signs** from Road Sign - Big Pack, **7 Service
Buildings**, bridge supports and `JumpTests`. Trees use imported prefab LODs;
signs use a scene-local URP material with the imported atlas. Vendor assets are
unchanged. Buildings are primitive shells/roofs, not final art. Close trunks and
bank faces supply parallax; 05's open middle remains the main place to judge whether
speed still feels too slow. Mobile rendering/performance remains unmeasured.

| Ramp | Length x width x rise |
| --- | --- |
| Wash embankment | 18 x 7 x 2.5 m |
| Ridge mound | 16 x 7 x 2.25 m |
| Crossover launch | 26 x 8 x 4.5 m |
| Hero embankment | 28 x 9 x 5.5 m |

These are solid, curved launch meshes with safe ground/deck below, not gaps requiring
a jump. Bike tuning is preserved: **22 m/s** normal, **35 m/s** turbo, acceleration
**18/35 m/s^2**, reverse **6 m/s**. V1 can lose substantial speed climbing a ramp without
Jump. Charge on the flat approach and release before the slope; late release can
lose eligibility. Slope attitude, launch and landing polish remain bike limitations.

EasyRoads3D Free remains Editor-only: native markers/source meshes live beneath
inactive `EditorOnly` `_RoadAuthoring`; runtime roads use `LevelGeometry/BakedAsphalt`.
Dirt is the scene's Unity Terrain. Each asphalt section has one collider-free paint
mesh: **18 cm** white edges inset **25 cm**, and **16 cm** yellow center dashes
(**4 m paint / 6 m gap**) where the road is at least 6 m wide. Paint follows the
native strip's exact edges/heights; no runtime marking system is involved.

To reshape this scene:

1. Activate `_RoadAuthoring`, hide `LevelGeometry/BakedAsphalt`, and edit native markers
   through EasyRoads' Inspector. Preserve matching source/baked names and transforms.
2. Run **Tools > Level Authoring > Update BikeRoute Asphalt Meshes**. The scene-only
   helper updates asphalt, colliders and markings in place, preserving asset GUIDs;
   it hides source authoring and enables the baked roads.
3. Conform/repaint the isolated Terrain and reposition/rebuild affected boundary
   meshes, trees, signs, buildings and ramps. **The mesh update does not move those
   objects or reshape Terrain.** Keep both rejoin surfaces and the underpass clear.
4. Save, run focused `BikeRouteGrayboxTests;BikeRouteGrayboxPlayTests;BikeRouteCorridorTests`,
   then drive all changed sections and both shortcuts. Check boundaries, jump
   approaches/landings, camera readability and turbo sightlines.

Avoid Free's destructive **Build Terrain/Finalize** workflow. Temporary authoring and
driving probes are archived outside Assets under `Logs/BikeRouteV3`; they are not
runtime generators. Focused test evidence is recorded there and under
`Logs/RepositoryAuditRemediation`. Automated traversal and screenshots establish
technical checks, not human driving feel or a stopwatch acceptance time. No full
regression or device validation is part of this pass.

V3 focused review: `BikeRouteV3FinalDrive` passed **8/8** (scene/startup, boundaries,
paint alignment, repeat bake and physics-driven main/both shortcut traversals). The
main route took about **4:11 simulated**, peaking at **34.6 m/s**. An earlier 6/6 pass
traversed all ramps without Jump; charged-jump review also completed, but V1 launch
quality still needs human review. Two repeated asphalt/paint updates caused no mesh
asset churn. The supported bridge shoulders, corrected deck-level warning sign and
lower shortcut passed the 7/7 `BikeRouteV3BridgeConfirm` follow-up. After removing
temporary probes, `BikeRouteV3Retained` passed 5/5 with a clean Editor exit. These
checks do not replace manual acceptance.

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
For another component-owned action, implement `IGameplayAction.TryExecute(player)` and
assign that component to **Action Target**. Execution is synchronous; returning false
rejects the visit before encounter/dialogue/objective actions. Invalid component assignments
log an error and reject execution. `ExcavatorRepairMission` delegates this contract to
its existing prerequisite-checked `Visit`; the reusable container has no mission dependency.

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
