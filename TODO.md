Kids VS Aliens — TODO

Live implementation roadmap.

Keep this file short and current.
PROJECT_CONTEXT.md contains the broader design/history.
AGENTS.md contains Codex working rules.

If this file conflicts with older roadmap text inside PROJECT_CONTEXT.md, this file wins for current priority/order.

NOW

Current override — 5 Sep 2026

Knowledge + Feedback + Pause V1 implementation and
review fixes are complete in code. Final compilation and all 43 Core EditMode tests
passed (0 failures, 0 skipped). Finish acceptance in the existing Unity project:

Open GamePoc and run Tools > Kids VS Aliens > Setup > Knowledge Feedback & Pause V1.

Inspect and save the gameplay scene manually; repeat for intended gameplay scenes.

Playtest Amy/Granny Knowledge overlays, feedback and nested manual pause ownership.

Verify Held/Charging grenade pause/resume, fresh FIRE, ammo/equipment preservation.

Verify preview rendering/lighting and menu framing; test safe-area/mobile input on device.

After hand-tuning UI, checkpoint before setup reruns: generated visual defaults are reapplied.

Details and remaining acceptance checklist: Assets/Game/Docs/07-GAMEPLAY-PRESENTATION.md.
Knowledge presentation V1 now precedes the older roadmap below. The existing repo
already has grenade gameplay; do not rebuild it from the historical goals below.

PRIOR ROADMAP / FOLLOW-UP GOALS

1. Grenade V1 — NEXT

Build a reusable grenade foundation from the existing project architecture.

Core goals

generic grenade item/data setup

pickup / inventory / use flow

throw input

spawn from player/hand

physics-based throw

collision / activation

configurable fuse or impact behavior

AoE hit resolution

damage / status-effect hook

clean integration with existing combat architecture

mobile-friendly / pool-friendly VFX path

support multiple grenade types from the same base architecture

Initial effect families

Electric / Shock

Fire

Ice

Toxic

Implementation order

placeholder grenade

throw

physics

activation

AoE gameplay effect

VFX

polish

A dedicated grenade implementation prompt/spec may override details here.

NEXT

2. Grenade Throw Animation Experiment

After gameplay works:

test existing grenade throw animation

test while rifle equipped

test while pistol equipped

test while unarmed / melee

first attempt: Animator Layer + Avatar Mask

keep gameplay independent from final animation polish

Do not over-invest if final animation assets will replace the setup.

3. Unarmed Combat V1

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

first tap must never feel delayed while waiting for a possible second tap

Future

charged Ultra moves

tune combo windows through playtesting

4. Generic Melee Weapon Framework

After unarmed combat works.

Support weapons such as

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

5. Ranged Alien V1

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

6. Knowledge Hologram / Tutorial Presentation

Finish the Knowledge Book presentation layer.

Desired flow

Gain Knowledge

hologram demonstration

Amy demonstrates technique

KNOWLEDGE ACQUIRED

control returns

Requirements

generic across weapon/melee/ability/alien-tech skills

short

skippable/replayable direction later

presentation should not own the actual skill-unlock logic

CURRENTLY DONE / CLOSED ENOUGH FOR V1

1. Plasma Pistol

equipped / dropped / menu preview

shooting

hitscan gameplay + visual plasma bolt

impact VFX

muzzle safety

skill gating

Pistol Handling Knowledge Book

2. Plasma Rifle

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

3. Melee Alien V1

NavMesh

perception/FOV/LOS

chase

investigation

melee attack

hit reaction

death

4. Core Mobile Foundation

Android build

mobile controls

auto aim

sticky lock / switching

LOS/cover

wall fading

NICE TO HAVE / PARKED

1. Rifle Left-Hand Grip / IK

rifle already contains LeftGripPoint

right hand remains real weapon attachment

add left-hand IK only after final rifle animations exist

not required for Rifle V1

2. Amy Short-Cover Aim-Point Priority

When low cover blocks the lower/center shot path:

prefer clear torso

then exposed upper body

lower body fallback

real Physics.Raycast remains authoritative

3. Circular Target Rail

Straight rail works.
Circular rail can come later.

4. Camera Feel Pass

Test

current

~5–10% closer

current-ish distance + subtle aim/movement look-ahead

Do this in real combat, not only practice range.

5. Pistol / Rifle Polish

Later

recoil

audio

reload

mechanical animation

battery/eject behavior

charged pistol

final weapon animation polish

PERFORMANCE / TECHNICAL LATER

1. Mobile Profiling

Profile before optimizing

CPU

GPU

draw calls

overdraw

VFX

physics

memory

thermal throttling

Reference device

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

Visually distinct grenade families may use their own base mesh, but must use the exact same automatic Blender -> Unity LOD workflow.

Grenade types should mainly differ through core/chamber/VFX/material treatment where possible.

Current Gravity Grenade reference

~22.3k triangles

~11.3k vertices

5 mesh objects

8 materials

Blender generation

Automatically output reusable visual LODs:

LOD0: hero mesh, current quality (~22k tris)

LOD1: ~8-12k tris

LOD2: ~2-4k tris

LOD generation should preserve the important silhouette and readable gameplay details while aggressively reducing geometry that is insignificant at distance.

Unity integration

Use Unity-compatible LOD naming.

Automatically detect/import generated LOD meshes.

Automatically create and populate the LODGroup.

Automatically assign sensible default transition thresholds.

No per-grenade manual renderer dragging or LOD configuration.

Gameplay logic, Rigidbody, colliders, damage and grenade state must remain independent from visual LOD switching.

Shared grenade bodies reuse the same LOD mesh assets instead of duplicating them per grenade type.

Materials / draw calls

Profile the current material cost.

Reduce/shared materials if the current 8-material setup becomes expensive.

Keep glass and emissive materials separate where required.

Prefer shared materials across grenade families whenever visually practical.

Mobile stress case

Profile on OnePlus Nord 3 with approximately:

38 grenade instances

12 characters

active combat

projectiles

grenade VFX

other scene VFX

Measure

CPU

GPU

draw calls

triangles

overdraw

memory

thermal behavior

VFX

Pool grenade VFX.

Allow grenade VFX complexity/effect density to scale with graphics presets where useful.

Visual LOD/VFX reduction should happen automatically where practical.

Goal

Solve the Blender -> Unity LOD/configuration pipeline once.

Do NOT manually create/configure three separate LOD setups for every grenade type.

Shared physical grenade bases reuse shared LODs.
Unique grenade bodies may have unique LOD meshes, but their generation/import/setup must still be automatic.

4. Graphics Presets

Create meaningful:

Low

Medium

High

Potential controls

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

Future gameplay systems that level work may need

ranged enemy

grenade

melee

alien beam / hoist

objectives/dialogue

combat encounter tools

FUTURE SYSTEMS

1. Alien Beam / Hoist

Reusable traversal prefab

capture/lift player

transport vertically

release

placeholder VFX first

custom VFX/animation later

2. Gauntlets

Future combat family

basic attack

combo

power attack

charged move

Ultra

Possible powers

shock

gravity

plasma/fire

freeze

telekinesis

knockback

3. Progression Expansion

Direction

Knowledge = permanent capability unlock

proficiency = primarily per story level/run

prevent early-level grinding from trivializing later content

meaningful level unlocks > tiny stat bumps

UI / Product Later

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
