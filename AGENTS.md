AGENTS.md — Kids VS Aliens

This file defines how Codex and other repo-aware coding agents should work in this repository.

Read PROJECT_CONTEXT.md and TODO.md before substantial implementation work.

Inspect Before Editing

Before changing code:

Inspect the actual relevant scripts, prefabs, ScriptableObjects, scenes, and references.

Trace how the existing system currently works.

Reuse existing architecture where practical.

Do not guess class names, serialized fields, prefab names, enum values, or folder structure.

If repository state conflicts with PROJECT_CONTEXT.md, treat the repository as source of truth for implementation details and report the discrepancy.

Do not propose or implement a clean-slate rewrite unless explicitly requested or the existing architecture demonstrably requires it.

Preserve Working Systems

Working systems should be considered production-sensitive.

Before changing shared code, check what depends on it.

Especially avoid regressions in:

player movement

mobile input

desktop input

auto aim / target switching

shooting

muzzle safety

combat hit resolution

pistol

rifle

inventory

Knowledge Books

skill gating

menu/catalog previews

enemy perception

LOS

wall fading

Android behavior

Prefer the smallest generic change that solves the actual problem.

Unity / Project Assumptions

Project:

Unity 6.5

URP

mobile-first

Android/iOS are first-class targets

desktop remains supported for development/testing

Do not introduce desktop-only assumptions into gameplay systems.

Avoid unnecessary per-frame allocations, expensive scene searches, uncontrolled Instantiate/Destroy loops, and other patterns that are obviously hostile to mobile performance.

Do not prematurely micro-optimize code that is not on a measured hot path.

Architecture Style

General rule:

Composition describes what something can do.
Inheritance describes what something is.

Prefer reusable components/services for shared capabilities such as:

damage

hit reactions

targeting

LOS

perception

shooting

status effects

item behavior

Avoid duplicating slightly different versions of the same combat/LOS/targeting logic across multiple enemy or weapon classes.

Do not build giant universal managers when a small reusable component is sufficient.

Data-Driven Configuration

Prefer existing ScriptableObject/data architecture for:

weapon stats

item references

prefab references

required skills

grenade/effect configuration

progression configuration

Avoid hardcoded prefab mappings or magic values when the project already has a suitable data layer.

Serialized Inspector values may intentionally differ from C# defaults.

Do not overwrite tuned Inspector values merely because the code default is different.

Physics Is Authoritative

Real Unity physics/world collision should remain authoritative for:

hits

cover

LOS

projectile interception

muzzle obstruction

grenade collision

Do not fake hits through walls or bypass real geometry unless an explicit gameplay rule requires it.

Camera Occlusion Fade

Current V2 direction is logical occluder-group fading rather than treating every
renderer as an independent blocker.

When working on this system:

preserve exact camera → player ray-based detection; avoid broad casts that create
false positives

resolve collider hits to a logical occluder group containing one or more renderers

use MaterialPropertyBlock or the existing equivalent; do not instantiate materials
per renderer

keep physics/colliders authoritative and active

preserve the existing aim/visibility semantics for camera-faded blockers

avoid one-time-only scene scans if runtime-added occluders must be supported

authoring/setup helpers must be idempotent

if the feature becomes a standard scene dependency, extend GameplaySceneSetup in
the same task

Do not automatically make every environment object fade. Focus on genuine large camera
blockers.

Weapons

Use the existing generic weapon pipeline.

Expected concepts include:

WeaponItemData

equipped prefab

dropped/world prefab

WeaponInstance

GripPoint

Muzzle

required skill

fire mode

animation style

Do not add weapon-specific logic to generic equipment/spawn systems unless there is no cleaner extension point.

The rifle's LeftGripPoint is currently parked for future support-hand IK. Do not implement/tune it unless explicitly requested.

PlasmaCore

The shared PlasmaCore system is already used by working weapons.

Do not modify shared PlasmaCore code merely to tune one weapon.

Prefer:

serialized config

prefab overrides

weapon-specific configuration

Only change the shared implementation when the change is genuinely reusable and safe for existing pistol/rifle usage.

Combat

Reuse the existing combat abstractions where relevant, including concepts such as:

HitInfo

CombatHitResolver

IDamageable

IHitReaction

Do not create parallel damage pipelines without first proving the existing one cannot support the feature.

Skills / Knowledge

Knowledge Books unlock capabilities.

Respect existing:

KnowledgeBookItemData

SkillData

PlayerSkillState

weapon required-skill gating

CURRENT DESIGN DIRECTION:

Knowledge / learned capabilities persist permanently.

Skill XP / proficiency is also currently intended to persist across deaths and
story-level retries.

Exact XP curves, anti-grind caps/diminishing returns and save implementation are not
locked yet.

Training/GamePoc progression must remain extremely slow or zero so it cannot become
the optimal persistent grind path.

This supersedes the older "proficiency primarily resets per run/story level" direction.

Do not redesign progression unless explicitly asked.
Do not implement a heavy save/meta-progression framework merely because this direction
is documented; first follow the live TODO and the explicit task.

Run / Replay / Persistence

The current campaign design is built around large handcrafted story levels, not
procedurally generated rooms.

Working retry rule:

death
→ restart the current large story level
→ permanent Knowledge + skill XP/proficiency remain

Mobile lifecycle interruption is a separate concern:

pausing, backgrounding, locking the phone or closing/reopening the app must not be
treated as gameplay death

when active-run persistence is implemented, suspend/resume must preserve the attempt
safely

keep meta progression, active-run state and platform suspend/resume conceptually
separate

Run-reset candidates include enemies, ammo, consumables, temporary buffs and most local
world state.

Physical gun/inventory persistence is intentionally NOT locked. Do not hardcode one
model into generic systems without an explicit task. The current design recommendation
to test first is permanent weapon Knowledge/progression plus run-specific physical
acquisition/loadout opportunities.

Avoid failure loops where death removes so much power that the next attempt becomes
materially harder.

Replay Variation

Do not introduce procedural geometry simply to create replayability.

Prefer lightweight authored variation such as:

different enemy compositions

elite/variant appearances

limited loot/reward variation

optional side encounters

route mastery / alternate routes where authored

possible earned shortcuts only if playtesting supports them

Post-Game Difficulty / World Tiers

After the full campaign is completed, the design requires an optional harder full-game
tier (working name: Invasion Tier 2 / New Game+).

If/when implemented:

reuse the same authored levels/scenes

use a data-driven global difficulty/tier profile

keep Tier 1 selectable

prefer encounter/behavior/variant changes in addition to numeric scaling

do not duplicate campaign scenes per tier

do not make the system only HP/damage multipliers if cleaner reusable hooks exist

Tier-exclusive enemies/rewards and Tier 3+ are optional later additions, not baseline
implementation requirements.

Grenades

The reusable grenade foundation and Electric VFX V1 already exist. Future grenade families or grenade polish should follow the live TODO or an explicit task prompt.

When working on grenades:

inspect existing inventory/item/combat/input systems first

prefer one reusable grenade architecture

keep effect behavior extensible

keep VFX pool-friendly

keep mobile performance in mind

do gameplay/physics first, polish second

Do not create one entirely separate architecture per grenade type unless behavior truly demands it.

Enemies

Reuse the existing enemy foundation:

NavMesh navigation

perception

FOV

LOS

investigation

hit/death handling

Role-specific behavior should remain modular.

A ranged enemy should be a new combat role on the existing enemy foundation, not a second enemy framework.

Animations

Animation work is currently provisional in several systems.

Do not over-engineer animation-layer/IK solutions before final animation assets exist.

Gameplay decides WHAT happens. Animation/presentation data decides HOW it looks.

Keep gameplay code independent from concrete clip names, Animator state names and authored frame timings where the existing semantic animation-action/event architecture can express the behavior.

When animation assets are replaced, prefer changing action mappings, Animator motions, Avatar Masks and authored animation events rather than rewriting gameplay/input/combat flow.

Where animation-facing issues are asset/import problems, prefer fixing them at the animation/FBX/import level rather than adding runtime rotation hacks.

Grenade release and player melee impact timing use authored typed animation events through the shared PlayerAnimation / CharacterAnimatorDriver / CharacterAnimationActions / CharacterAnimationEventRelay path.

Do not replace those authored markers with gameplay-side magic delays.

If blending becomes fragile, keep gameplay working and park visual polish.

Menus / Preview Prefabs

Menu preview presentation should not force gameplay-prefab changes.

Prefer menu-preview-specific wrappers/settings for:

framing

scale

rotation

camera presentation

Keep catalog/menu entries data-driven.

Art / Large Assets

Do not modify files under large Art/model/texture areas unless the task explicitly requires it.

Do not regenerate or replace production assets casually.

Large binary assets may use Git LFS.

Keep Unity .meta files intact and tracked.

If an LFS-managed Unity asset appears corrupt/unreadable, first verify that the actual binary was pulled rather than only an LFS pointer.

Scene / Prefab Safety

When editing Unity YAML or serialized assets directly:

be conservative

preserve GUIDs

preserve references

do not mass-rewrite unrelated serialized data

prefer editor-safe/script-based changes when manual YAML editing is risky

If a task depends on Inspector-only state that cannot be safely inferred from text files, say so and specify what must be checked in Unity.

Canonical Gameplay Scene Setup / Repair

The canonical setup/repair path for gameplay scenes is:

Tools > Kids VS Aliens > Setup > Setup or Repair Active Gameplay Scene

The central helper is responsible for making old and new gameplay scenes receive the standard shared player/presentation wiring without level designers remembering manual Inspector steps.

Whenever a feature adds a required gameplay-scene or player dependency, shared controller, presentation root, reference, or required component, extend the central GameplaySceneSetup helper in the SAME task.

Rules:

the helper must remain idempotent and safe to rerun

reuse existing objects/components and repair references rather than creating duplicates

preserve intentional/tuned scene content wherever possible

reuse feature-specific setup helpers from the central helper instead of duplicating their setup logic

existing gameplay scenes must be repairable by rerunning the central helper

new gameplay scenes must not require undocumented manual wiring for standard systems

do not leave a required per-scene setup step outside the helper unless it is intentionally level-specific content

when practical, verify the helper on GamePoc and at least one other gameplay scene after changing standard scene requirements

Testing Expectations

After implementation, provide a short test checklist.

Prefer:

smallest relevant Editor test

regression test for affected existing systems

Android/device test when the change is mobile-sensitive

Do not claim a Unity behavior is verified if it has only been inferred from code.

Clearly distinguish:

implemented

code-reviewed

needs Unity test

needs device test

Scope Discipline

Do not expand the task into unrelated cleanup.

Do not refactor unrelated systems just because they could be cleaner.

If you notice a useful unrelated improvement:

mention it briefly

do not implement it unless it is required or explicitly requested

The project follows:

small working V1
→ test
→ fix root cause
→ polish
→ expand

Communication Style

Be concise and implementation-focused.

When reporting work:

state what changed

state which files changed

state any architectural decision that matters

state what must be tested in Unity

Avoid long generic tutorials unless asked.

If there are multiple valid implementations, recommend one and explain the tradeoff briefly.

Do not pretend certainty when repository state is ambiguous.

Source of Truth Priority

When sources conflict, use this priority:

explicit current task prompt

actual repository code/assets

TODO.md

current-status overrides in PROJECT_CONTEXT.md

older/historical design context inside PROJECT_CONTEXT.md

Do not silently follow outdated roadmap text when newer status overrides exist.
