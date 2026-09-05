Kids VS Aliens — TODO

Live implementation roadmap.

Keep this file short and current.
PROJECT_CONTEXT.md contains the broader design/history.
AGENTS.md contains Codex working rules.

If this file conflicts with older roadmap text inside PROJECT_CONTEXT.md, this file wins for current priority/order.

NOW

1. Grenade Throw Animation Experiment — NEXT

Grenade gameplay is already working. Electric Grenade VFX V1 is now closed enough for V1.

Next task is to test the existing grenade throw animation in the real player setup without coupling gameplay logic to animation polish.

Test cases

unarmed / melee

plasma pistol equipped

plasma rifle equipped

First implementation attempt

Use:

Animator Layer

Avatar Mask

upper-body throw animation where possible

existing locomotion continues underneath

grenade gameplay / release timing remains controlled by gameplay code

Goals

Amy can throw while moving

throw does not break locomotion

throw does not permanently disturb weapon pose

pistol/rifle return cleanly to their normal animation state

grenade release can be synchronized to a clean animation event / authored release point

gameplay still works if the animation is missing or interrupted

keep the solution generic enough for Granny and future characters

Important

Do not over-invest yet.

This is an experiment to determine whether the existing throw animation can work cleanly across:

Unarmed

Pistol

Rifle

If one shared upper-body layer looks bad for one weapon family, split animation handling by weapon stance rather than forcing one animation to fit everything.

2. Knowledge + Feedback + Pause V1 — Acceptance

Implementation and review fixes are complete in code.

Final compilation and all 43 Core EditMode tests passed:

0 failures

0 skipped

Remaining Unity acceptance:

Open GamePoc

Run Tools > Kids VS Aliens > Setup > Knowledge Feedback & Pause V1

Inspect and save the gameplay scene manually

Repeat for intended gameplay scenes

Playtest Amy/Granny Knowledge overlays, feedback and nested manual pause ownership

Verify Held/Charging grenade pause/resume, fresh FIRE, ammo/equipment preservation

Verify preview rendering/lighting and menu framing

Test safe-area/mobile input on device

After hand-tuning UI, checkpoint before setup reruns because generated visual defaults are reapplied

Details and remaining acceptance checklist:

Assets/Game/Docs/07-GAMEPLAY-PRESENTATION.md

CURRENTLY DONE / CLOSED ENOUGH FOR V1

1. Grenade Gameplay Foundation

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

2. Plasma Pistol

equipped / dropped / menu preview

shooting

hitscan gameplay + visual plasma bolt

impact VFX

muzzle safety

skill gating

Pistol Handling Knowledge Book

3. Plasma Rifle

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

4. Melee Alien V1

NavMesh

perception/FOV/LOS

chase

investigation

melee attack

hit reaction

death

5. Core Mobile Foundation

Android build

mobile controls

auto aim

sticky lock / switching

LOS/cover

wall fading

NEXT

1. Unarmed Combat V1

Build the first real player melee foundation.

Goals

punch

punch chain

kick

kick chain

responsive input

reusable hit resolution

proficiency/skill-family integration where appropriate

Current mobile input idea

tap attack
→ punch immediately

second tap inside short window
→ transition into kick chain

continued taps
→ continue combo

hold
→ future Ultra

Important

First tap must never feel delayed while waiting for a possible second tap.

Future

charged Ultra moves

tune combo windows through playtesting

2. Generic Melee Weapon Framework

After unarmed combat works.

Support weapons such as:

baseball bat

crowbar

pipe

golf club

wooden stick

alien / plasma blade

Goals

common melee hit architecture

reusable attack framework

item/equipment integration

weapon-specific tuning without one-off duplicated systems

3. Ranged Alien V1

Add the second real enemy combat role.

Goals

reuse current enemy navigation/perception/LOS/investigation foundation

keep ranged combat role modular

maintain distance / reposition

pressure Amy out of static cover

visible/readable projectile attacks

combine well with melee rusher

begin making cover/fences tactically important

Do not build a separate enemy framework.

4. Knowledge Hologram / Tutorial Presentation

Finish the Knowledge Book presentation layer.

Desired flow

Gain Knowledge
→ hologram demonstration
→ Amy demonstrates technique
→ KNOWLEDGE ACQUIRED
→ control returns

Requirements

generic across weapon/melee/ability/alien-tech skills

short

skippable/replayable direction later

presentation should not own the actual skill-unlock logic

NICE TO HAVE / PARKED

1. Rifle Left-Hand Grip / IK

Rifle already contains LeftGripPoint.

right hand remains real weapon attachment

add left-hand IK only after final rifle animations exist

not required for Rifle V1

2. Amy Short-Cover Aim-Point Priority

When low cover blocks the lower/center shot path:

prefer clear torso

then exposed upper body

lower body fallback

Real Physics.Raycast remains authoritative.

3. Circular Target Rail

Straight rail works.
Circular rail can come later.

4. Camera Feel Pass

Test:

current

~5–10% closer

current-ish distance + subtle aim/movement look-ahead

Do this in real combat, not only practice range.

5. Pistol / Rifle Polish

Later:

recoil

audio

reload

mechanical animation

battery/eject behavior

charged pistol

final weapon animation polish

PERFORMANCE / TECHNICAL LATER

1. Mobile Profiling

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

2. Pooling

Pool frequent gameplay effects when justified:

plasma bolts

muzzle VFX

impacts

grenade effects

enemy projectiles

Avoid building a giant universal pooling framework prematurely.

3. Grenade Asset LOD / Mobile Rendering Pass

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

4. Graphics Presets

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

5. Android / Build Checks

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

1. Alien Beam / Hoist

Reusable traversal prefab:

capture/lift player

transport vertically

release

placeholder VFX first

custom VFX/animation later

2. Gauntlets

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

3. Progression Expansion

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

test in Unity

test on Android when relevant

fix root causes

polish later

Do not expand scope during implementation unless the extra work is required for the feature to be correct.
