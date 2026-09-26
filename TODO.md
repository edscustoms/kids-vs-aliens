Kids VS Aliens — TODO

Live implementation roadmap.

Keep this file short and current.
PROJECT_CONTEXT.md contains the broader design/history.
AGENTS.md contains Codex working rules.

If this file conflicts with older roadmap text inside PROJECT_CONTEXT.md, this file wins for current priority/order.

NOW

IMPLEMENTED — Combat Economy V1 — 26 Sep 2026

Shared Plasma Capsules fund empty-magazine auto reloads; Armor Capsules have separate
storage/pickups without a consumption mechanic yet. Duplicate plasma weapons convert
through world pickup acquisition, and equipped plasma aliens can roll capsule loot
without changing scavenged-weapon custody. The existing HUD shows magazine/resources/
reload state. Active Run preserves quantities, all magazines and paid reload timers.
Starting values, test evidence and the manual checklist: Docs/CombatEconomyV1.md.
Final combat balance, armor consumption and device acceptance remain separate work.

CURRENT FOCUS — NEXT ~2 WEEKS

CURRENT STATUS UPDATE — 18 Sep 2026

IMPLEMENTED — Centralized Audio V1 — 23 Sep 2026

SoundEvent/AudioLibrary, pooled AudioService, local AudioEmitter, Audio Library window,
MainAudioMixer, opt-in mobile import presets and validator are implemented. Pistol fire,
confirmed melee contact and Beam phases are wired. Starter audio now supplies six live
Placeholder events and twelve unused Candidates; only Beam_Loop remains Missing.
Combat Economy adds a separate placeholder dry-trigger event, bringing the runtime
manifest to eight wired events; unused Candidates remain Editor-only.
Shared UI_Click/UI_PlayConfirm obey the final-launch rule, with pooled transition tails.
Central UI audio excludes gameplay action inputs; navigation and inventory/quick slots still click.
Gameplay and menu scene repair include audio. See Docs/StarterAudioPack.md and
Docs/AudioSystem.md for workflow, executed checks and pending full-scene/device listening.
Other audio integrations and legacy Starter Assets footstep migration remain deferred.

IMPLEMENTED — Interface / active-run persistence

The existing menu now uses conditional Play -> Active Run Found, with Continue and
confirmed New Game replacement. HUD, Pause, full inventory/quick-slot drag/drop,
Knowledge review/unread feedback and the reusable tutorial floor use the shared theme.
Main-menu selectors, Amy preview and text-only buttons remain in place.

Permanent Knowledge/XP/acknowledgements and active-run snapshots are separate.
Safe Quit, periodic/mutation/lifecycle saves, paused foreground return and explicit
resume entry are implemented. Current player, inventory/equipment/ammo, pickups,
chests, enemies and spawners restore functional state. Future mission/door/set-piece
components use IRunStateParticipant; no objective counter architecture was added.
Death, confirmed Hard Restart and confirmed New Game replacement discard active state
only. Canonical scene repair includes the new dependencies and stable identities.

Validation and remaining device acceptance are recorded in Docs/RunInterface.md.
Do not reopen this as a save/UI rewrite; extend the existing implementation.

DONE WITH EXTRA TODO — Camera Occlusion Fade V2

The V2 production direction is now implemented and working around logical occluder
grouping, exact camera → Amy sampling, stable hysteresis, MaterialPropertyBlock-driven
fade and production authoring/setup. Treat the current working system as
production-sensitive rather than an open rewrite target.

Extra TODO:

keep validating/tuning against real Construction Site blockers

retain Android/device regression testing whenever fade/material work changes

only revisit architecture if a real readability/performance problem appears

DONE WITH EXTRA TODO — Alien Beam / Hoist V1

Manual upward Beam Hoist is now working as a reusable production system.

Locked rules:

normal Jump activates manual hoist when Amy is inside a valid hoist start area and
Beam Hoist Knowledge is unlocked

hoist movement is one continuous cubic Bézier from the real start position directly
to the landing position

release/control1/control2 only shape that curve

do not reintroduce a separate vertical phase, reset, second curve or teleport

automatic level-start Beam arrival is a separate use and must not receive the manual
hoist-start holographic pad

Authoring is generic:

BeamHoistSurface
→ BeamHoistSurfaceBaker
→ fixed baked valid lower approach/start cells
→ gameplay + Beam Hoist zone presentation use the same baked data

BeamHoistAbility gameplay limits are the source of truth for the bake. There is no
independent approach-width tuning that may drift away from gameplay reach.

New BeamHoistSurface instances in a configured gameplay scene automatically refresh/bake
through the standard authoring/setup workflow. Runtime-added surfaces must carry their
authored bake; no runtime baker should reshape the area.

The hoist-start hologram is also implemented:

manual upward hoist only

completely hidden before Beam Hoist Knowledge

hidden while far away

fades in only when Amy comes within configurable Reveal Distance

fades out beyond configurable Hide Distance with hysteresis

fixed world-space footprint; player movement never moves/resizes/rebuilds it

exact baked allowed start area is the visual source of truth

current idle glow/look is approved; do not redesign it casually

when Amy is in a valid start position, the current stronger flicker/power-up is approved

hoist start hides the pad and the real Beam Transport VFX takes over

genuinely disconnected valid pockets stay separate

contiguous valid regions should visually read as one connected pad without filling
invalid gaps

Floating presentation is integrated for Beam transport/hoist and genuine long falls.
The runtime semantic path is good; presentation remains swappable.

Extra TODO:

preserve the visually preferred/original Floating import behavior and make sure setup
tooling does not silently force an importer configuration that changes the motion

finish/verify the connected-region visual merge if the current Astra pass is not yet
closed

add/verify the Beam Hoist Knowledge tutorial demonstration after presentation is final

polish BeamTransportVFX sparks later; do not touch working movement for VFX polish

investigate the automatic LevelStart/BeamInSpawn placement issue independently:
moving the authored start can leave Amy elsewhere and the beam visible

investigate the pre-existing desktop Space/Jump lock independently from Beam Hoist

perform Android/device regression checks for the final zone/floating presentation

DONE WITH EXTRA TODO — Knowledge + Feedback + Pause + Tutorial Presentation V1

The generic framework is working and should not be rebuilt.

Extra TODO:

add a proper Grenade learned-action tutorial demonstration

add a proper Beam Hoist learned-action tutorial demonstration

audit Pistol / Rifle / Fighting demonstrations and keep them semantic/swappable

final control/tutorial UI polish belongs to the later HUD/control redesign pass

IMMEDIATE ADDITION — LEVEL 1 GAME LOOP / EXCAVATOR ESCAPE — ABSOLUTE MUST

Keep the existing roadmap below intact. This is an added near-term gameplay-loop requirement.

Level 1 core framing:

Amy wakes up on the Construction Site after being beamed into the starting location.

The main Level 1 goal is to get out of the Construction Site / find a way toward school.

The entire playable Level 1 remains on the Construction Site for now.
Do not add school gameplay/interactions yet.

Target first/early successful run:

roughly 12–20 minutes

mastered/replay target:

roughly 6–10 minutes

Inside that main goal, keep changing the immediate task roughly every 1–3 minutes so the
level does not become one long traversal/combat sequence.

Use short, concrete site-specific tasks such as:

find a weapon / useful item

reach or clear a route

survive a combat encounter

retrieve a required construction-site item

activate / repair a piece of machinery

move through a new functional area of the site

The first run should be genuinely interesting, but failure should also make the player
want to retry because they understand the route/encounters better.

MUST-HAVE SET-PIECE — BREAK YOUR WAY OUT WITH THE EXCAVATOR

A Construction Site exit/route is physically blocked by stacked concrete roadblocks /
barriers.

Do not solve this specific escape beat with a random key or keycard.

Amy must repair/activate the excavator and use it to physically clear the obstruction.

Repair objective:

REPAIR EXCAVATOR — 0/3

Battery cables / jumper leads

Hydraulic fluid

Fuse

Each required item should be placed in a believable Construction Site zone so the task
naturally makes the player move through/use the site, for example:

battery cables → maintenance / electrical / workshop area

hydraulic fluid → machinery service / fuel area

fuse → site office / electrical storage area

V1 interaction must stay deliberately simple:

find required item
→ pick it up
→ bring it to the excavator repair/drop-off zone
→ matching repair step completes automatically
→ show basic text/status feedback

Example feedback:

Battery cables installed — 1/3

Hydraulic fluid added — 2/3

Fuse replaced — 3/3

Do not build bespoke cable-connecting, fluid-pouring, fuse-insertion or mechanic repair
animations for V1.

After 3/3:

excavator becomes activatable

excavator base can remain static

arm/bucket only needs simple authored rotation/translation

arm/bucket is authoritative for the set-piece and physically pushes/topples the stacked
concrete roadblocks with Rigidbody physics

use sound / dust / impact VFX / camera feedback later as needed

do not build a complex real-time wall-fracture/destruction system for this V1 beat

progression must not depend entirely on perfect physics; add a deterministic exit-clear
check/fail-safe so a badly wedged barrier can never soft-lock the level

This excavator escape is now a must-have Level 1 gameplay beat and should be implemented
relatively early rather than treated as distant polish.

Two parallel tracks are the current priority.

A. Camera Occlusion Fade V2 — ABSOLUTE MUST

Replace the current renderer-centric blocker logic with logical occluder groups so
multi-part construction props/buildings fade as one gameplay-readable blocker.

Target architecture:

FadeWhenBlockingPlayer
→ CameraFadeOccluder logical group
→ Renderer[] controlled through MaterialPropertyBlock

Detection direction:

keep exact camera → player sample rays; do NOT return to broad SphereCastAll logic

use about five player samples: head / shoulders / torso / hips

ray hits resolve collider → CameraFadeOccluder group

a configurable sample threshold decides whether the group is blocking

smooth fade toward roughly 0.05–0.10 visibility

short clear-delay / hysteresis around 0.10–0.20 s to prevent flicker

colliders and gameplay remain active

preserve current PlayerAim semantics so camera-faded blockers can still be ignored
for aim visibility where intended

use property blocks; do not instantiate materials per renderer

support runtime registration; do not depend only on one scene-wide startup scan

Android URP/Lit fade issue — confirmed:

Editor fade works, but stock URP/Lit occluders remain visually opaque on Android.

Device logs confirmed that blocker detection and fade state are correct on Android:
ENTER / EXIT fires correctly, alpha reaches roughly 0.05 / 1.0, Surface is switched to
Transparent, blend factors are correct, ZWrite is 0, render queue is 3000 and
\_SURFACE_TYPE_TRANSPARENT is enabled.

The problem is the runtime conversion of authored Opaque URP/Lit materials to
Transparent. Android player builds can strip unused transparent Lit shader variants,
so runtime material values can change correctly while the compiled shader still renders
effectively opaque.

Production solution:

create a custom URP Lit Fade shader based on URP Lit

add a built-in \_Fade property with default value 1.0

implement fade through opaque dither / clip logic that is always compiled into the shader

keep environment blocker materials Opaque permanently

Camera Occlusion controls only \_Fade; do not switch Surface Type / render queue / blend
state at runtime for supported environment materials

add an editor migration tool that replaces relevant environment URP/Lit materials with
URP Lit Fade while preserving compatible Lit properties/textures

do not blindly convert characters, weapons, VFX or other Lit materials that can never
act as camera blockers

Authoring helper target:

Tools > Level Tools > Configure Camera Occluders

Helper should:

add/reuse CameraFadeOccluder

collect child renderers

configure relevant collider child layers

validate that camera-blocking materials/shaders support \_Fade

warn on incompatible renderers/materials

be idempotent and safe to rerun

Do not make every object fade. Prioritize walls, roofs, cabins, trucks, containers,
large scaffold/building sections and other true camera blockers. Ground, floors and
small cover normally should not fade.

B. Construction Site + Replay-Loop Prototype — ABSOLUTE MUST

Current initial route is locked:

START / Amy wake-up
→ forced single route THROUGH unfinished construction building
→ alien / rocket crash zone

This is the only initial path.

After the crash reveal, build toward:

crash zone
→ wider construction encounters
→ locked school-backyard gate
→ objective sends player toward site/helper office / key
→ key pickup
→ return to gate
→ school backyard

Do not allow the key/site-office route directly from the start.

First validation target:

roughly 10–15 minutes of genuinely playable content

use real enemies / objectives / traversal, not only geometry

replay the same slice around 5–10 times

compare Attempt 1 vs Attempt 2 / 5 / 10

the later attempt should feel faster, more skillful and more intentional

Primary acceptance question:

"After I die, do I actually want to run this again?"

If the answer becomes "this is a chore", adjust route, encounter or progression design
before building a 45–90+ minute full authored level.

ABSOLUTE MUSTS — CORE LOOP / MOBILE

Keep large handcrafted story locations. Do not redesign the campaign into procedural rooms.

Working death rule: death restarts the current large story level from the beginning.

Mobile pause/background/app close must NEVER count as death.

Active-run suspend/resume is implemented; finish the remaining release/device checks
listed in Docs/RunInterface.md.

ACTIVE-RUN SUSPEND / RESUME — ABSOLUTE MUST

This is stricter than ordinary checkpoint saving.

Incoming calls, app backgrounding, locking the phone, OS interruption/termination where
recoverable, normal manual quit and closing/reopening the app must preserve the current
attempt.

On relaunch, Continue must restore the same functional run state and place Amy back where
the run was suspended.

Only two intentional actions discard the active run:

death
→ delete/reset active-run state
→ restart the current large story level from the beginning

Hard Restart from the in-game menu
→ explicit confirmation
→ delete/reset active-run state
→ restart the current large story level from the beginning

Hard Restart should live in the in-game pause/menu near Options/Settings. Ordinary Quit
must NOT behave like Hard Restart.

Keep permanent/meta progression separate from the active-run snapshot.

At minimum, the active-run architecture must be able to preserve meaningful state such as:

current scene/level and Amy transform/facing

player health and relevant run-specific player state

equipped/owned run items as finally decided by the inventory persistence model

current objective and objective progress

picked-up/consumed mission items

doors/gates/interactables already changed

enemy dead/alive and other meaningful encounter state

mission/set-piece state such as excavator repair 0/3 → 3/3

spawned/removed required mission objects

temporary/run-local progression that must survive a normal suspend

Do not attempt frame-perfect serialization of particles, animation frames or transient
projectiles. Resume must be functionally equivalent and safe.

The current player is effectively in god mode, so death-triggered reset can be wired and
tested later, but the persistence architecture must support death as an explicit active-run
discard path from the start.

Knowledge / learned skills persist permanently.

Skill XP / proficiency currently persists across deaths/retries.

Failure must not create a downward power spiral where the next attempt is weaker.

Repeated attempts must become faster/smarter through mastery, progression and route
knowledge.

Combat readability, touch controls, camera visibility and objective clarity are
release-critical.

Build natural 5–15 minute gameplay chunks even when the total level is much longer.

TO TEST / THINK ABOUT BEFORE LOCKING

Whole-level restart viability

Current working target:

Construction Site first/early run: about 12–20 min when complete

mastered Construction Site: about 6–10 min

full large authored level: potentially about 45–90+ min if playtesting supports it

Do not treat these as fixed numbers.

Gun / inventory persistence

Test three models:

permanent physical guns

re-find physical guns each attempt

hybrid — permanent weapon Knowledge/progression + run-specific acquisition/loadout

Current recommendation to test first: hybrid.

Never create a Dust & Neon-style death spiral where losing a strong gun makes the next
run materially harder.

Permanent XP anti-grind

Need to test one or more of:

diminishing XP from repeatedly farming trivial encounters

caps linked to Knowledge/story milestones

strongly reduced or zero Training/GamePoc XP

meaningful unlocks rather than endless tiny stat scaling

Anti-repetition systems

Test lightweight authored variation before procedural generation:

changing enemy compositions

elite/variant appearances

limited loot/reward variation

optional side pockets / risk-reward encounters

route mastery / alternate paths

possible earned permanent shortcuts

Permanent shortcuts are NOT locked. Test whether they preserve tension or undermine the
whole-level-restart idea.

Camera / mobile feel

Continue the separate camera feel test later:

current framing

~5–10% closer

current-ish distance + subtle look-ahead

Test only in real combat / real construction geometry.

POST-GAME — ABSOLUTE MUST LATER

After the player completes the full campaign, unlock a selectable harder full-game tier
(working name: Invasion Tier 2 / New Game+).

Requirements:

same authored levels; do NOT duplicate campaign scenes

Tier 1 remains selectable

use a global data-driven difficulty/tier profile

increase challenge across the game

harder enemy compositions / more elites

fair/readable aggression or behavior increases where useful

stronger/new attack patterns where practical

tighter resources where appropriate

avoid pure HP-sponge / damage-sponge scaling

POST-GAME — NICE TO HAVE

new Tier-2-only enemies or enemy variants

tier-exclusive rewards / cosmetics / unlocks

additional hazards or remixed encounter rules

Tier 3+ only if Tier 2 proves fun and worth maintaining

Gameplay Scene Setup Rule — permanent project convention

Canonical command:

Tools > Setup > Setup or Repair Active Gameplay Scene

Whenever a new standard player/scene dependency, gameplay presentation system,
controller, shared reference or required scene component is added, extend the central
GameplaySceneSetup helper in the same task.

Requirements:

idempotent and safe to rerun

repair/reuse existing objects and references instead of creating duplicates

reuse feature-specific setup helpers where appropriate

existing gameplay scenes must be repairable through the central command

standard scene wiring must not be left as undocumented manual Inspector work

when practical, verify changes on GamePoc and at least one older gameplay scene

Current gameplay acceptance / cleanup

Grenade Throw Animation V1 is accepted enough for V1. The semantic action path,
typed 0.54 s release marker, charge preservation, interruption/fallback handling and
Amy/Granny-compatible shared setup are implemented. Current motion is acceptable for
V1; final animation polish is parked.

Knowledge + Feedback + Pause + tutorial presentation V1 is working. The central
GameplaySceneSetup helper has been verified on another gameplay level and repairs the
standard presentation/melee scene stack. Further UI/device polish can happen later.

Unarmed Combat V0 is working end-to-end with a real melee enemy:

Fighting Knowledge Book / skill unlock

Fighting inventory option

plain unarmed FIRE enters combat stance and attacks when the skill is known

valid enemy within about 2.0 m automatically keeps/enters combat stance when eligible

no nearby enemy + more than about 3 seconds without melee input returns to normal
UnarmedLocomotion

weapon/grenade routing remains authoritative

semantic melee actions + authored MeleeImpact markers drive damage timing

real melee enemies take damage, react and die

Current FightingIdle/Boxing animations are placeholders and visibly need replacement.
Do not rebuild melee gameplay merely to improve those animations.

CURRENTLY DONE / CLOSED ENOUGH FOR V1

Grenade Gameplay Foundation

Reusable grenade gameplay already exists.

Current foundation includes:

grenade item/data flow

pickup / inventory / use flow

throw input

spawn / held grenade handling

physics-based throw

collision / activation

configurable grenade gameplay behavior

AoE gameplay effect path

damage / status-effect integration

pause/resume handling while grenade is Held/Charging

ammo/equipment preservation

mobile-friendly gameplay path

Do not rebuild the grenade foundation from older roadmap text.

Electric Grenade VFX V1

Closed enough for V1.

Current visual stack:

compact white/cyan energy core

halo

expanding/fading shock ring

fractal/jagged lightning

16 radial arcs

10 secondary arcs

target arcs

ground crawlers

4 hero bolts

10 ignition spikes

26 energy streaks

reusable electric energy cloud

irregular overlapping cloud pockets

bright wisps

pooled/reused runtime VFX architecture

Final cleanup removed the obsolete Ionized Mist path.

The completed V5 generator was retired in the helper cleanup pass. Its authored
prefab/material output remains; edit those assets for any future approved polish.

Do not continue polishing Electric Grenade VFX until the broader game V1 polish pass unless a real gameplay/readability problem appears.

Future grenade effect families

After animation / core combat priorities:

Fire

Ice

Toxic

Possible later families:

Shock variants

Gravity / pull

Healing

Sticky

Smoke / confusion

EMP

Plasma Pistol

equipped / dropped / menu preview

shooting

hitscan gameplay + visual plasma bolt

impact VFX

muzzle safety

skill gating

Pistol Handling Knowledge Book

Plasma Rifle

rifle asset

materials

PlasmaCore

dropped prefab

equipped prefab

menu preview prefab

automatic fire

rifle animation style

menu item/catalog integration

Rifle Handling skill

Rifle Handling Knowledge Book

required skill wired

Melee Alien V1

NavMesh

perception/FOV/LOS

chase

investigation

melee attack

hit reaction

death

Core Mobile Foundation

Android build

mobile controls

auto aim

sticky lock / switching

LOS/cover

wall fading

Grenade Throw Animation V1

Semantic grenade action path is integrated through PlayerAnimation /
CharacterAnimatorDriver / CharacterAnimationActions.

Authored typed release marker commits the physical throw once.

Charge, held visual, inventory/equipment preservation, fallback and interruption
handling are implemented.

Current animation is acceptable for V1; final visual polish is parked.

Knowledge + Feedback + Pause + Tutorial Presentation V1

Knowledge tutorial/popup, feedback and pause/suspension foundations are implemented.

The learned action is demonstrated by the current character through the existing
semantic animation/presentation path.

Standard gameplay-scene wiring is now repaired through the central
Setup or Repair Active Gameplay Scene command.

Unarmed Combat V0 Foundation

Working end-to-end in a real gameplay level.

Includes:

Fighting Knowledge Book and permanent skill unlock

Fighting inventory option

plain-unarmed FIRE melee activation

automatic combat stance near a valid enemy at about 2.0 m

about 3 second inactivity exit when no enemy is nearby

separate combat locomotion stance without replacing normal UnarmedLocomotion

semantic melee action mappings

authored MeleeImpact damage timing

lean one-input attack buffering/sequence

existing CombatHitResolver / damage / hit-reaction path

weapon and grenade FIRE priority preserved

Current combat animations are placeholder-quality and are intentionally swappable
without changing PlayerMeleeController/input/targeting/damage/skill/inventory flow.

NEXT AFTER CURRENT FOCUS

Generic Melee Weapon Framework

Build reusable melee support for weapons such as:

baseball bat

crowbar

pipe

golf club

wooden stick

alien / plasma blade

Goals:

reuse current combat hit abstractions

reuse the semantic animation/action-event approach where appropriate

item/equipment integration

weapon-specific tuning without one-off duplicated combat systems

keep player unarmed V0 working while this expands

Ranged Alien V1

Add the second real enemy combat role.

Goals:

reuse current enemy navigation/perception/LOS/investigation foundation

keep ranged combat role modular

maintain distance / reposition

pressure Amy out of static cover

visible/readable projectile attacks

combine well with melee rusher

begin making cover/fences tactically important

Do not build a separate enemy framework.

Unarmed Combat V1 Polish — parked until needed

V0 mechanics are proven. Improve presentation without rewriting gameplay.

Later:

replace current FightingIdle / Boxing placeholder animations

better attack blending/transitions

better impact feel, VFX/SFX and optional hit-stop/camera impulse after playtesting

kicks and kick chains

combo tuning

later Ultras/proficiency expansion

Animation replacement rule:

gameplay code must remain independent from clip/state names and authored frame timing

swap animation presentation through mappings/Animator motions/Avatar Masks and
authored MeleeImpact events

NICE TO HAVE / PARKED

Rifle Left-Hand Grip / IK

Rifle already contains LeftGripPoint.

right hand remains real weapon attachment

add left-hand IK only after final rifle animations exist

not required for Rifle V1

Amy Short-Cover Aim-Point Priority

When low cover blocks the lower/center shot path:

prefer clear torso

then exposed upper body

lower body fallback

Real Physics.Raycast remains authoritative.

Circular Target Rail

Straight rail works.
Circular rail can come later.

Camera Feel Pass

Test:

current

~5–10% closer

current-ish distance + subtle aim/movement look-ahead

Do this in real combat, not only practice range.

Pistol / Rifle Polish

Later:

recoil

audio

reload

mechanical animation

battery/eject behavior

charged pistol

final weapon animation polish

Held-Item Attachment Authoring Cleanup

Current GripPoint alignment remains valid for V1.

Later simplify authoring so held-item roots attach 1:1 to the shared WeaponSocket
(local position/rotation zero) and per-item visual positioning lives on a child visual
offset.

Goal:

WeaponSocket
→ HeldItem root at zero
→ Visual child offset

This should make grenade/weapon/item pose authoring easier to understand and remove
the need to reason about inverse GripPoint alignment when tuning held presentation.

Do not refactor the working attachment system during unrelated V1 work.

PERFORMANCE / TECHNICAL LATER

Mobile Profiling

Profile before optimizing:

CPU

GPU

draw calls

overdraw

VFX

physics

memory

thermal throttling

Reference device:

OnePlus Nord 3

Then test older Android hardware.

Pooling

Pool frequent gameplay effects when justified:

plasma bolts

muzzle VFX

impacts

grenade effects

enemy projectiles

Avoid building a giant universal pooling framework prematurely.

Grenade Asset LOD / Mobile Rendering Pass

Build this once as a reusable grenade optimization pipeline so future grenade types do not require separate manual LOD setup/configuration.

Architecture

Reusable grenade LOD pipeline, not necessarily one universal grenade mesh.

Grenades that share the same physical base should reuse the same body/lever/pin/ring meshes and the same generated LOD meshes.

Visually distinct grenade families may use their own base mesh, but must use the exact same automatic Blender → Unity LOD workflow.

Grenade types should mainly differ through core/chamber/VFX/material treatment where possible.

Current Gravity Grenade reference

~22.3k triangles

~11.3k vertices

5 mesh objects

8 materials

Blender generation

Automatically output reusable visual LODs:

LOD0: hero mesh, current quality (~22k tris)

LOD1: ~8–12k tris

LOD2: ~2–4k tris

LOD generation should preserve the important silhouette and readable gameplay details while aggressively reducing geometry that is insignificant at distance.

Unity integration

use Unity-compatible LOD naming

automatically detect/import generated LOD meshes

automatically create and populate the LODGroup

automatically assign sensible default transition thresholds

no per-grenade manual renderer dragging or LOD configuration

gameplay logic, Rigidbody, colliders, damage and grenade state remain independent from visual LOD switching

shared grenade bodies reuse the same LOD mesh assets instead of duplicating them per grenade type

Materials / draw calls

profile the current material cost

reduce/shared materials if the current 8-material setup becomes expensive

keep glass and emissive materials separate where required

prefer shared materials across grenade families whenever visually practical

Mobile stress case

Profile on OnePlus Nord 3 with approximately:

38 grenade instances

12 characters

active combat

projectiles

grenade VFX

other scene VFX

Measure:

CPU

GPU

draw calls

triangles

overdraw

memory

thermal behavior

VFX

pool grenade VFX

allow grenade VFX complexity/effect density to scale with graphics presets where useful

visual LOD/VFX reduction should happen automatically where practical

Goal

Solve the Blender → Unity LOD/configuration pipeline once.

Do not manually create/configure three separate LOD setups for every grenade type.

Shared physical grenade bases reuse shared LODs.
Unique grenade bodies may have unique LOD meshes, but their generation/import/setup must still be automatic.

Graphics Presets

Create meaningful:

Low

Medium

High

Potential controls:

render scale

shadows

shadow distance / resolution

particles

VFX complexity

bloom / post-processing

effect density

Android / Build Checks

keep editor-baked target MeshCollider workflow

do not revert to unnecessary FBX Read/Write

use Android Logcat during device debugging

LEVEL / CONTENT WORK

A collaborator currently owns most level/environment construction.

Core coding work should support level creation rather than taking it over unless explicitly requested.

Future gameplay systems that level work may need:

ranged enemy

grenade

melee

alien beam / hoist

objectives/dialogue

combat encounter tools

FUTURE SYSTEMS

Alien Beam / Hoist

DONE WITH EXTRA TODO — this historical future item is now implemented as the reusable
Beam Transport + manual upward Beam Hoist system described in the 18 Sep current-status
override above. Keep the historical bullets below as original intent/context.

Reusable traversal prefab:

capture/lift player

transport vertically

release

placeholder VFX first

custom VFX/animation later

Gauntlets

Future combat family:

basic attack

combo

power attack

charged move

Ultra

Possible powers:

shock

gravity

plasma/fire

freeze

telekinesis

knockback

Progression Expansion

Direction:

Knowledge = permanent capability unlock

skill XP / proficiency = currently permanent across deaths/retries

prevent early-level grinding from trivializing later content

Training/GamePoc must be extremely slow or zero for persistent progression

meaningful skill/ability unlocks > endless tiny stat bumps

keep meta progression, active-run state and mobile suspend/resume as separate concerns

UI / PRODUCT LATER

objective presentation

dialogue/subtitles

pause/settings

achievements/leaderboards

analytics

loading-background safe framing

final app icon

DEVELOPMENT RULE

For every major feature:

inspect current architecture

design smallest compatible V1

implement

extend GameplaySceneSetup in the same task if the feature adds any new standard
player/scene dependency or required wiring

test in Unity

test the central scene repair helper on an existing gameplay scene when its standard
requirements changed

test on Android when relevant

fix root causes

polish later

Do not expand scope during implementation unless the extra work is required for the feature to be correct.
