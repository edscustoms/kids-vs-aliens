Kids VS Aliens — Project Context

Source basis: This file contains the full contents of GAMEPLAN_UPDATED_V3(1).md below, preserved as project context.

Important: The section immediately below contains current implementation/status overrides that are newer than parts of the original gameplan. Where an override conflicts with the older text below, the override wins for current implementation status and immediate priorities. The older text is still preserved because it contains useful design rationale, story direction, system intent, and historical context.

For live implementation order, use TODO.md.
For Codex coding/repo rules, use AGENTS.md.

CURRENT IMPLEMENTATION OVERRIDE — 6 Sep 2026 — Unarmed Combat V0 + Gameplay Scene Setup

Unarmed Combat V0 is now working end-to-end on Amy in a real gameplay level with
moving melee enemies. The Fighting Knowledge Book unlocks the permanent Fighting
skill and makes the Fighting inventory option available. Plain unarmed FIRE can enter
combat stance and perform semantic melee attacks when the skill is known. Fighting
selection intentionally switches away from weapon use without creating a parallel
equipment framework.

Combat stance behavior is now:

eligible empty-handed/Fighting player + FIRE anywhere -> combat stance + melee attack

eligible player + valid enemy within about 2.0 m -> combat stance automatically

nearby enemy or recent melee input keeps the stance active

no nearby enemy and more than about 3 seconds without melee input -> normal
UnarmedLocomotion

weapon/grenade selection remains authoritative and must not be overridden by melee

The current melee implementation is intentionally lean. PlayerMeleeController owns the
small V0 melee state/input sequence, while existing inventory, aiming, animation,
Knowledge and CombatHitResolver systems retain their responsibilities. Melee actions
use semantic CharacterActionId mappings and authored typed MeleeImpact animation
events; gameplay does not depend on the current Mixamo clip names or hardcoded hit
timings. Real melee enemies were verified to take punch damage, react and die.

The current FightingIdle / Boxing punch animations are placeholder-quality and visibly
need replacement/polish. Treat animation assets as swappable presentation: replacing
them later should require action/Animator mapping and authored impact-event changes,
not rewrites of melee gameplay/input/targeting/damage/skill/inventory flow. Kicks,
kick chains, Ultras, proficiency expansion, final impact feel/VFX/SFX and final combat
animation polish remain later work.

Gameplay scene setup is now centralized through:
Tools > Kids VS Aliens > Setup > Setup or Repair Active Gameplay Scene

The helper was used successfully to repair another gameplay level so the Knowledge
tutorial/popup, feedback/pause stack and current melee requirements work there. This
helper is now a project convention: whenever a new standard player/scene dependency is
introduced, extend the central GameplaySceneSetup helper in the same task. It must stay
idempotent, reuse/repair existing objects and references, and prevent level designers
from having to remember repeated Inspector wiring.

Practice/cardboard targets are not currently a reliable validation target for grenade
or melee damage; use real combat enemies when validating the melee damage path.

CURRENT IMPLEMENTATION OVERRIDE — 6 Sep 2026

Grenade animation now uses the existing PlayerAnimation / CharacterAnimatorDriver /
CharacterAnimationActions path. FIRE release snapshots charge and enters Throwing;
the authored typed release marker commits the existing physical grenade launch once.
Selection/held visual and inventory remain intact through wind-up. Missing animation
uses immediate gameplay fallback; interrupted wind-up returns to Held safely.
The generator authors the marker, and standard character setup adds the generic
Animator relay and compatible shared mapping. The generated motion now includes a
raised backward wind-up, forward arm extension, a 0.54 s release marker and eased
recovery. Physics/VFX retain their existing behavior. Actual Editor captures of Amy
and Granny were reviewed over unarmed locomotion in ten input directions/blends.
Walking/running currently share normalized locomotion input. See
Assets/Game/Docs/08-GRENADE-ANIMATION.md for captures and remaining Play Mode/device checks.
Compilation and 18 focused tests pass, including native release position at 30/60 FPS.
The broader open-Editor Core run passed 55/59; four existing frame-dependent tests
could not advance Time.frameCount. Earlier existing-project batch validation was 57/57.

CURRENT IMPLEMENTATION OVERRIDE — 5 Sep 2026

Knowledge + Feedback + Pause V1 code, initial assets and an idempotent Editor setup
command are implemented. The prior architecture is preserved. Manual suspension
keeps selected grenades held, cancels charge safely, and requires fresh FIRE after
resume. Desktop Escape complements the mobile Pause/Play button. Knowledge uses
the current character and actual equipment with existing stance animations.
Preview cameras explicitly reuse the feature-free Mobile_Renderer; desktop
gameplay SSAO/default renderer remain unchanged.

Final runtime/Editor compilation and the full Core EditMode suite passed: 43 tests,
0 failures, 0 skipped, including integrated Knowledge Book/manual-pause ownership.
Scene wiring is generated by Tools > Kids VS Aliens > Setup > Knowledge Feedback
& Pause V1; gameplay scenes have NOT been auto-saved or migrated. Setup reruns
reapply generated UI visual defaults, so checkpoint before rerunning after polish.
Play Mode, GPU rendering and Android/iOS acceptance are still required.
See Assets/Game/Docs/07-GAMEPLAY-PRESENTATION.md for exact setup and validation.

The actual repo already contains grenade gameplay, inventory, knowledge gating,
inert recovery and carry visuals. Older "Grenade V1 next" text below is historical;
use the current TODO integration checks before returning to the broader roadmap.

CURRENT STATUS OVERRIDES — 3 Sep 2026

Team / ownership

Core mechanics, combat, weapons, grenades, melee, skills/Knowledge Books, menus, enemy systems, gameplay architecture, mobile systems and related implementation remain the primary coding lane.

A collaborator is now handling most level/environment construction and pacing work.

Do not assume level generation/layout is the next coding task unless explicitly requested.

Rifle — V1 implemented

The rifle is no longer an unbuilt future item.

Current rifle work includes:

rifle asset imported and prepared

logical rifle material regions

reusable PlasmaCore integration

Plasma_Rifle_Dropped

Plasma_Rifle_Equipped

Plasma_Rifle_MenuPreview

PlasmaRifleItem

automatic fire

rifle animation style

menu preview item/catalog integration

RifleHandling

Rifle Handling Knowledge Book

weapon skill requirement wired

Current equipped structure includes:

Plasma_Rifle_Equipped
├── Muzzle
├── GripPoint
├── LeftGripPoint
└── PlasmaCore

The weapon is attached through the normal right-hand WeaponSocket / GripPoint architecture.

LeftGripPoint is parked as a future support-hand IK target.

Rifle polish intentionally parked

Second-hand IK / left-hand grip is a nice-to-have later, because final rifle animations do not exist yet. Do not spend time tuning this before final animation assets unless explicitly requested.

Knowledge / progression direction

Permanent Knowledge and combat proficiency are now treated as separate ideas.

Permanent

Knowledge Books permanently unlock what Amy knows how to do:

weapon handling

combat techniques

gauntlets

alien technology

abilities / gameplay verbs

Primarily per-level / per-run

Combat proficiency should primarily grow during the active story level/run so repeatedly grinding early content cannot permanently trivialize later levels.

Examples:

Fists

Kicks

Pistol

Rifle

Gauntlets

future melee families

Exact XP curves/reset behavior still require playtesting.

Training/GamePoc progression must be extremely slow and must never become the optimal grind path.

Current immediate roadmap

The current broad implementation order/status is:

Grenade gameplay + Electric VFX V1 — closed enough for V1

Grenade throw animation V1 — implemented/accepted enough for V1; visual polish later

Knowledge + Feedback + Pause + tutorial presentation V1 — implemented; central scene repair helper now owns standard scene wiring

Unarmed Combat V0 — working end-to-end; placeholder combat animations and broader melee polish parked for later V1

Generic melee weapon framework

Ranged alien V1

Use TODO.md for the live order if priorities change.

Grenade direction

Grenades are the next major system.

A separate dedicated grenade implementation prompt may be supplied, so do not infer a detailed implementation solely from this context document.

Broad requirements:

generic reusable grenade architecture

throw

physics

collision / activation

area effect

pool-friendly/mobile-conscious VFX architecture

multiple effect families from one shared system where practical

Current planned effect families:

Electric / Shock

Fire

Ice

Toxic

Possible later:

gravity/pull

healing

sticky

smoke/confusion

EMP

Grenade animation direction

A grenade throw animation exists.

Later experiment:

rifle equipped

pistol equipped

unarmed/melee

Preferred first experiment:

Animator Layer

Avatar Mask

Do not over-invest if final animation assets make the setup temporary.

Unarmed combat direction

After Grenade V1:

punches

punch chains

kicks

kick chains

later charged Ultra moves

Current mobile input idea:

first tap punches immediately

second tap inside a short window can transition into kick chain

continued taps continue the chain

long hold can become Ultra later

Do not delay the first attack while waiting to detect a double tap.

Generic melee weapons

After unarmed combat, build a reusable melee framework for weapons such as:

bat

crowbar

pipe

golf club

stick

alien/plasma blade

Prefer common melee hit/attack architecture over one-off weapon scripts.

Ranged alien

Ranged alien remains planned, but comes after the current grenade/melee priorities unless the live TODO changes.

Reuse the existing enemy architecture rather than creating a second enemy framework.

Android / MeshCollider

The earlier runtime MeshCollider / Read-Write issue was resolved through an editor-side collider baking workflow.

Do not revert production FBXs to requiring Read/Write merely to support runtime collider cooking unless there is a specific reason.

Current development principle

Before implementing a new feature:

inspect existing architecture
↓
reuse what already exists
↓
build the smallest working V1
↓
test in Unity
↓
fix root causes
↓
polish only after the mechanic works

ORIGINAL GAMEPLAN / DESIGN CONTEXT — PRESERVED IN FULL

Kids VS Aliens — Level 1 Gameplan

Purpose

Build the first real playable level as a small vertical slice, not a huge finished map.

Prove:

gameplay flow

story opening

enemy encounters

camera feel

objectives/guidance

HUD structure

progression direction

Keep the environment generic first: basic walls, floors, rooms and corridors. Do not lock the setting to a school yet.

Current Implementation Status — V1 Foundations

The following core systems are now working well enough to treat as V1 locked unless playtesting exposes a real problem.

Player / Aim / Combat

Mobile auto-aim with sticky target lock.

Mobile free-look/manual target switching.

Desktop mouse aiming.

When no active aim input exists, Amy naturally faces the movement direction.

Plasma pistol firing is impact-synchronized:

visible bolt reaches target

impact VFX

damage / health update

Hit or Death presentation

Muzzle safety prevents shooting through solid cover when the weapon clips into a wall.

Transparent fence-style obstacles can be aimed and shot through while still physically blocking movement.

Knowledge / Skills

Knowledge Book world pickup + inventory flow works.

Book can unlock a referenced SkillData.

First test skill: Pistol Handling.

Pistol use can be gated behind owning the required skill.

Skill XP/state architecture exists.

Debug XP tester can add XP and prove skill leveling behavior.

Current direction: skill progression is game-wide/permanent rather than resetting every level.

Chest / Loot

Reusable alien chest POC works.

Separate proximity/hold interaction opens the chest.

Chest can spawn multiple loot objects without stacking them at the same position.

Loot uses the existing world-pickup/inventory flow.

Chest/book rarity direction: Common → Uncommon → Rare → Epic → Legendary.

Current POC chest and knowledge-book visuals are good enough for gameplay testing.

Enemy — Melee V1

The first real melee alien enemy is now functional:

NavMesh navigation.

Per-enemy movement/turn/avoidance variation.

Idle/wander behavior.

Initial perception + FOV + close awareness.

LOS handling.

Vision through chain-link fences while solid walls still block LOS.

Chase and approach spacing.

Last-known-position investigation after losing sight.

Directional investigation when hit from an unseen/far-away direction.

Multiple enemies do not all behave like one synchronized bot.

Melee attack with a temporary Right Hook animation.

Hit reaction animation that interrupts movement.

Killing hit goes directly to Death.

Death animation → corpse remains ~3 seconds → slow fade → destroy.

Real alien humanoid visual is retargeted through Unity Humanoid.

Enemy Animator Controller is now HumanoidMeleeEnemy.

Level / Navigation Tooling

Level 1 greybox exists and is being used as the real gameplay test scene.

NavMesh baking is working across the current level geometry.

Modular floor/wall layout tools are usable.

Smart fence system V1 is complete enough for production prototyping.

Fence runs support physical collision while being independently configurable for vision/projectile pass-through behavior.

Scene Setup

Create a branch:

feature/level01-prototype

Duplicate:

GamePoc
→ Level01_Prototype

Keep the working framework objects:

Player

camera

input/mobile controls

combat systems

wall fade

lighting/volume baseline

required managers

Remove:

practice targets

rails

debug/test-only objects

GamePoc remains the permanent sandbox.

Opening

Start simple and mysterious:

BLACK SCREEN
↓
alien beam carries Amy downward
↓
Amy reaches the floor
↓
environment gradually reveals
↓
Amy wakes / gets up
↓
"...Where am I?"
↓
player gets control

No exposition dump.

Wake-up Area Direction

The initial beam-in area should preferably feel open to the sky / unobstructed, so the spaceship beam makes visual sense.

Do not lock the exact setting yet.

If Level 1 becomes a school:

Amy beams down onto the playground / outside area.

First progression leads toward the school.

A possible early objective is reaching her classroom / locker area to recover her stuff.

From there the level can expand naturally through the school and surrounding areas.

Story Direction

Main goal of the game

Get home to your parents.

The journey home should be broken into short, progressive missions/quests so Amy always has an understandable immediate goal even while the larger mystery keeps expanding.

Alien resource conflict

The aliens did not come to Earth randomly: their own world is in serious danger and they need resources from other planets to survive/save it.

Earth contains something they desperately need.

The exact resource is not locked yet.

Best current direction:

use a fictional deep-earth mineral / isotope / planetary-energy resource rather than something generic like gold or water;

the resource is tied to a planet's long-term geological / magnetic / ecological stability;

taking a limited amount may be survivable;

industrial-scale extraction eventually destabilizes Earth badly enough that the planet can become uninhabitable or effectively die.

This creates the central alien conflict:

Faction A — survival at any cost

Their homeworld comes first.

Other planets/races are expendable if that is what saving their own people requires.

They are willing to drain Earth even knowing what the long-term result will be.

They can still believe they are morally justified: their own families/world are dying too.

Faction B — preservation / rebellion

They also want to save their homeworld.

They do not believe another inhabited world should be sacrificed to achieve it.

They want controlled extraction, another resource source, another technology, or some alternative solution.

They oppose the eradication of other civilizations even when doing so makes saving their own world harder.

The conflict should therefore avoid becoming simply:

good aliens vs evil aliens

Both factions want their world to survive. The real disagreement is:

How much is your own survival worth if the price is somebody else's entire world?

Hidden truth

Amy was abducted by aliens.

Alien hypnotic/control technology does not work properly on kids.

Alien factions are divided by the resource-extraction conflict.

An internal rebellion exists.

A rebel alien is secretly helping Amy.

Some mysterious beaming/teleportation is actually rebel assistance.

Amy initially thinks she is only escaping/surviving and trying to get home.

She gradually discovers Earth itself is being harvested.

The player slowly realizes Amy is also helping one side of an alien civil conflict.

Later reveals can show that the hostile faction is acting out of desperation rather than random conquest.

Reveal this gradually. No exposition dump. The player should understand the stakes piece by piece through environments, missions, alien behavior, intercepted information and rebel assistance.

Resource / Core Extraction Direction

Lock the broad concept:

The aliens are extracting a fictional deep-earth resource tied to planetary core stability.

Their own world is running out of it / has lost enough of it that the planet is becoming unstable or dying. Earth contains large deposits, especially deep below the surface.

Small-scale extraction may be survivable.

Industrial-scale extraction is not.

If enough is removed:

Earth's core / internal energy balance becomes unstable

magnetic protection can weaken

earthquakes / tectonic instability increase

strange atmospheric and aurora effects begin appearing

ecosystems start failing

eventually Earth can become uninhabitable or effectively die

The exact scientific explanation can remain fictional but should feel internally consistent.

Still decide later:

exact resource name

whether it is a mineral, isotope, crystalline material or alien-described form of planetary energy

what specifically happened to the alien homeworld

how quickly Earth's damage becomes irreversible

whether the rebel faction already knows of an alternative solution or is gambling on finding one

Core Extraction Visual Direction

This should become one of the game's major visual reveals.

Think enormous alien extraction infrastructure drilling deep into Earth, far beyond normal human scale:

surface / facility
↓
massive vertical excavation structure
↓
deep-earth alien machinery
↓
huge rotating / pulsing drill systems
↓
energy conduits carrying extracted material upward
↓
glowing fractures / heat / strange atmospheric effects
↓
the player gradually realizes the machine is not mining a normal resource
— it is damaging the planet itself

Visual ideas:

colossal drill shafts disappearing kilometers downward

alien machinery spanning entire caverns

giant moving mechanical rings around the central drill

deep magenta/cyan/alien-green energy conduits

molten rock / extreme heat deeper underground

crystalline or luminous resource veins being stripped from the surrounding rock

tremors and falling debris as extraction intensifies

abnormal auroras or sky effects above major extraction sites

distant landscape fractures / dying vegetation around heavily harvested zones

enormous beams or transport columns moving extracted material toward ships

Future Core-Extraction Level

At some point, build an authored level around one of these gigantic extraction sites.

Possible progression:

enter apparently normal alien facility
↓
descend through increasingly industrial sections
↓
see machinery getting larger and less understandable
↓
first view into a gigantic underground shaft
↓
realize the installation extends deep into Earth
↓
fight through extraction infrastructure
↓
reach a massive core-drilling chamber
↓
major story reveal: Earth is being actively drained

This can be a major mid-game story moment rather than something explained early.

The scale should make Amy feel tiny.

The player should understand the stakes visually before or while the story confirms them:

these machines are not just stealing resources — they are slowly killing Earth.

Level Flow

Do not force the whole level to progress only up/right.

Use controlled macro progression, but allow local movement and combat in all directions.

Example:

START
↓
exploration / clue
↓
first danger
↓
weapon / useful item
↓
real combat area
↓
objective / route choice
↓
harder encounter
↓
final area / boss

Possible later structure:

Start 1 ─┐
Start 2 ─┼─→ exploration / encounters → Final Area
Start 3 ─┘

This can support exploration and replayability.

First Playable Slice

Build only the first 3–5 minutes initially:

Amy arrives/wakes up.

Explore a small safe area.

Find a strange clue.

Receive a small objective.

Face the first threat.

Possibly find an improvised/melee option.

Discover alien weapon tech.

Enter the first real combat encounter.

See a strange beam/help event or mystery clue.

Stop and evaluate before expanding.

Enemies

Need at least two meaningful roles.

Enemy 1 — Melee Rusher — V1 DONE

Current alien melee enemy:

navigates around walls/obstacles with NavMesh

has slightly varied movement/avoidance behavior per enemy

idles/wanders when unaware

detects Amy through FOV/LOS/close awareness

sees through chain-link fences

chases Amy and uses close-range approach spacing

investigates the last known position after LOS is lost

if shot without seeing Amy, investigates in the incoming-shot direction rather than magically knowing Amy's position

attacks at close range

uses temporary humanoid locomotion + Right Hook + Hit + Death animations

pauses movement during Hit reaction

death stays on the floor briefly, fades, then disappears

Animator Controller:

HumanoidMeleeEnemy

Enemy 2 — Different role — TODO

Likely ranged:

uses distance/cover

forces a different response

combines well with the melee Rusher

Reuse the common enemy architecture where possible, but keep role-specific combat/animation logic separate.

Prototype visuals are fine. Behavior matters first.

UI / Communication

Dialogue / Subtitles

Support:

speaker

text

duration

optional audio clip (defo include in architecture to easily add later)

later localization

subtitles ON/OFF

subtitle size Small / Medium / Large

Default: ON.

Objectives / Guidance

Example:

NEW OBJECTIVE
Find a way out

Then shrink/fade into a subtle HUD objective.

Optional subtle world markers can come later.

Context Hints

Only for controls/mechanics when needed.

Tutorial prompts should be driven by tester feedback later, not added everywhere by default.

HUD Direction

Leave movement controls mostly as-is for now.

Refine:

health

current objective

weapon

grenade

melee/item slots

subtle guidance

options dropdown

Options dropdown:

resume

settings

restart

main menu

Deeper HUD/info screens may pause gameplay so the player can read safely.

Planned Gameplay Systems

Parallel to level work:

rifle

grenade

player melee / combos / Ultra moves

alien beam/hoist transport

second enemy role (likely ranged)

objective/dialogue presentation

free-look + auto-shoot experiment (when both thumbs are on the both toggles and you encounter an enemy in visual range => autoshoot)

auto-aim refinement from playtesting

skill unlock/level progression expansion

new mechanics as needed

Already proven at V1:

chest/container system

knowledge/book unlock system

skill XP/state baseline

first melee enemy AI

Knowledge / Book Unlocks

Books can act as progression items that teach the player new skills or techniques.

Example:

Find "Baseball Bat" book
↓
book enters inventory
↓
player chooses Use / Gain Knowledge
↓
hologram demonstration plays
↓
Amy demonstrates the new bat technique
↓
KNOWLEDGE ACQUIRED
↓
skill / technique is unlocked

Keep the system generic so books/knowledge can unlock:

weapon techniques

melee moves

alien technology

abilities

gameplay mechanics

Current V1 proof:

Pistol Handling Knowledge Book
↓
pickup / inventory
↓
Use / Gain Knowledge
↓
Pistol Handling skill acquired
↓
pistol use becomes available
↓
skill can gain XP / levels

Skill XP / Leveling

Knowledge/books unlock a skill. Using that skill afterward can improve it through XP and levels.

Core rule:

unlock skill through knowledge
↓
use skill in real gameplay
↓
gain XP
↓
skill levels up
↓
later levels can improve / expand that skill

XP gain depends heavily on where the player is playing:

Story / Normal Gameplay

Normal XP progression speed.

This is the intended/main way to level skills.

Repeated real combat and meaningful use of a skill should naturally progress it.

Practice / Training Course

XP gain is very slow compared with real gameplay.

Training should be useful for learning mechanics and practicing aim/combat, but should not become the optimal farming method.

Practice-course XP is awarded only while Hardcore Mode is enabled.

Normal/non-Hardcore training gives no progression XP.

Design goal:

Real missions are where Amy meaningfully grows. Training can contribute a little, but only when the player accepts the extra Hardcore risk.

The exact XP multipliers, level curves and per-action XP values are not locked yet and should be tuned through playtesting.

Knowledge tutorial presentation V1 now uses the current character to demonstrate the learned action directly; a separate hologram effect is not required for V1. Further presentation polish can come later.

Learning something should still feel like an event rather than a generic skill-tree click.

Chest — V1 DONE

Reusable alien container:

opening interaction is separate from the chest logic

can be empty

can contain one or multiple items

loot positions are separated so drops do not stack on each other

loot spawns away from Amy so it is not instantly auto-picked up

uses normal dropped-world-item pickup flow

rarity-compatible glow structure exists

final premium chest art/polish comes later

Alien Beam / Hoist 100% doing this in a generic way

Large top-to-bottom alien beam:

captures/lifts the player

moves them to another vertical location

releases them

later gets custom animation/VFX

This is a planned feature.

Camera Test

After a real gameplay scene exists, compare:

Current camera

About 5–10% closer

Current-ish distance + subtle smooth look-ahead toward aim direction

Judge:

combat readability

movement feel

enemy visibility

cover

weapon visibility

mobile screen usage

Do not lock the final camera from the practice range.

Progression / Save Direction

Not locked yet.

Current possibilities:

Career Mode = story/progression

Training Mode = GamePoc sandbox

levels may have multiple entry/start zones

routes can converge toward a final boss/objective

later decide checkpoints/safe zones vs stronger roguelite restart structure

Avoid building a heavy save system before the level loop is proven.

Development Rule

small area
→ play
→ adjust
→ add next area
→ play
→ adjust

Do not build the entire level before testing.

The first priority is making the opening and first encounter feel like a real game, not a tech demo.
