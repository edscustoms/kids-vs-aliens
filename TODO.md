Kids VS Aliens — TODO

Live implementation roadmap.

Keep this file short and current.
PROJECT_CONTEXT.md contains the broader design/history.
AGENTS.md contains Codex working rules.

If this file conflicts with older roadmap text inside PROJECT_CONTEXT.md, this file wins for current priority/order.

NOW

1. Grenade V1 — NEXT

Build a reusable grenade foundation from the existing project architecture.

Core goals:

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

Initial effect families:

Electric / Shock

Fire

Ice

Toxic

Implementation order:

placeholder grenade
↓
throw
↓
physics
↓
activation
↓
AoE gameplay effect
↓
VFX
↓
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

Goals:

punch

punch chain

kick

kick chain

responsive input

reusable hit resolution

proficiency/skill-family integration where appropriate

Current mobile input idea:

tap attack
→ punch immediately

second tap inside short window
→ transition into kick chain

continued taps
→ continue combo

hold
→ future Ultra

Important:

first tap must never feel delayed while waiting for a possible second tap

Future:

charged Ultra moves

tune combo windows through playtesting

4. Generic Melee Weapon Framework

After unarmed combat works.

Support weapons such as:

baseball bat

crowbar

pipe

golf club

wooden stick

alien / plasma blade

Goals:

common melee hit architecture

reusable attack framework

item/equipment integration

weapon-specific tuning without one-off duplicated systems

5. Ranged Alien V1

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

6. Knowledge Hologram / Tutorial Presentation

Finish the Knowledge Book presentation layer.

Desired flow:

Gain Knowledge
↓
hologram demonstration
↓
Amy demonstrates technique
↓
KNOWLEDGE ACQUIRED
↓
control returns

Requirements:

generic across weapon/melee/ability/alien-tech skills

short

skippable/replayable direction later

presentation should not own the actual skill-unlock logic

CURRENTLY DONE / CLOSED ENOUGH FOR V1

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

NICE TO HAVE / PARKED

Rifle Left-Hand Grip / IK

rifle already contains LeftGripPoint

right hand remains real weapon attachment

add left-hand IK only after final rifle animations exist

not required for Rifle V1

Amy Short-Cover Aim-Point Priority

When low cover blocks the lower/center shot path:

prefer clear torso

then exposed upper body

lower body fallback

real Physics.Raycast remains authoritative

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
↓
design smallest compatible V1
↓
implement
↓
test in Unity
↓
test on Android when relevant
↓
fix root causes
↓
polish later

Do not expand scope during implementation unless the extra work is required for the feature to be correct.
