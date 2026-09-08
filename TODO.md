Kids VS Aliens — TODO

Live implementation roadmap.

Keep this file short and current.
PROJECT_CONTEXT.md contains the broader design/history.
AGENTS.md contains Codex working rules.

If this file conflicts with older roadmap text inside PROJECT_CONTEXT.md, this file wins for current priority/order.

NOW

CURRENT FOCUS — NEXT ~2 WEEKS

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

Authoring helper target:

Tools > Kids VS Aliens > Level Tools > Configure Camera Occluders

Helper should:

add/reuse CameraFadeOccluder

collect child renderers

configure relevant collider child layers

validate that materials/shaders support \_Fade

warn on incompatible renderers

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

Keep large handcrafted story locations. Do not redesign KVA into procedural rooms.

Working death rule: death restarts the current large story level from the beginning.

Mobile pause/background/app close must NEVER count as death.

Add safe active-run suspend/resume before release.

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

Tools > Kids VS Aliens > Setup > Setup or Repair Active Gameplay Scene

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

Current setup command:

Tools > Kids VS Aliens > Setup > Electric Grenade VFX V5

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
