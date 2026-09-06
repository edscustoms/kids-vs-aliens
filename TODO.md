Kids VS Aliens — TODO

Live implementation roadmap.

Keep this file short and current.
PROJECT_CONTEXT.md contains the broader design/history.
AGENTS.md contains Codex working rules.

If this file conflicts with older roadmap text inside PROJECT_CONTEXT.md, this file wins for current priority/order.

NOW

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

NEXT

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

proficiency = primarily per story level/run

prevent early-level grinding from trivializing later content

meaningful level unlocks > tiny stat bumps

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
