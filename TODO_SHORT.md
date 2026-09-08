Kids VS Aliens — Short TODO

NOW — Next ~2 Weeks

1. Camera Occlusion Fade V2 — ABSOLUTE MUST

Move from renderer-centric fading to logical CameraFadeOccluder groups.

Keep exact camera → player rays; use ~5 body samples.

Fade whole logical blockers smoothly with short hysteresis.

Keep colliders/gameplay active.

Preserve PlayerAim blocker semantics.

Use MaterialPropertyBlock; no per-renderer material instances.

Add an idempotent level-authoring helper for occluder setup/validation.

Test against real construction-site geometry.

2. Construction Site + Replay Test — ABSOLUTE MUST

Initial route:

START → unfinished construction building → crash zone

That is the only initial route.

Then build/test:

crash zone → wider site → locked school gate → key/site office → return → backyard

First target:

~10–15 minutes of real playable content

replay it ~5–10 times

Attempt 5 should feel faster/smarter than Attempt 1

if replay feels like chores, fix the loop before expanding the full level

ABSOLUTE MUSTS — Core Game

Large handcrafted story levels; do not turn KVA into procedural-room runs.

Working death rule: death restarts the current large story level.

Mobile interruption/app background must never equal death.

Safe active-run suspend/resume before release.

Knowledge / learned skills persist permanently.

Skill XP / proficiency currently persists across retries.

No death spiral where failure makes the next run weaker.

Replays must gain speed/mastery/meaning, not just repeat identical content.

Natural ~5–15 minute mobile-friendly play chunks inside longer levels.

Combat readability, camera visibility, touch controls and objective clarity are critical.

Completing the full campaign unlocks optional harder Invasion Tier 2 / New Game+.

TO TEST / THINK ABOUT

Replay / pacing

Construction Site target: ~12–20 min first/early run, ~6–10 min mastered.

Full authored level could be ~45–90+ min only if replay testing proves it works.

Test route mastery, alternate paths, encounter variants, loot variation and risk/reward side areas.

Test earned/permanent shortcuts; do not lock them until we know they preserve tension.

Guns / inventory

Test:

permanent physical guns

re-find physical guns each attempt

hybrid: permanent weapon Knowledge/progression + run-specific acquisition/loadout

Current recommendation to test first: hybrid.

XP / grind

Test diminishing returns, story/Knowledge caps and very-low/zero Training XP so early
content cannot become the optimal permanent grind.

POST-GAME — TIER 2

Must

same campaign / same authored levels

Tier 1 remains selectable

global data-driven difficulty profile; no duplicated scenes

harder enemy compositions / elites / fair aggression

avoid difficulty that is only HP ×2 / damage ×2

Nice to Have

new Tier-2-only enemies / variants

new attacks / hazards

exclusive rewards / cosmetics / unlocks

Tier 3+ later only if Tier 2 proves worthwhile

DONE / CLOSED ENOUGH FOR V1

Grenade Gameplay Foundation

Electric Grenade VFX V1

Grenade Throw Animation V1

Knowledge + Feedback + Pause + Tutorial Presentation V1

Plasma Pistol

Plasma Rifle

Melee Alien V1

Core Mobile Foundation

Unarmed Combat V0 Foundation

Current Amy fighting animations remain placeholder-quality.

NEXT AFTER CURRENT FOCUS

Generic Melee Weapon Framework

Reusable bats, pipes, crowbars, sticks, alien blades, etc., using the existing combat
and semantic animation-event foundations.

Ranged Alien V1

Reuse current enemy navigation/perception/LOS/investigation architecture. Add a modular,
readable ranged role that pressures Amy out of static cover.

NICE TO HAVE / PARKED

Unarmed Combat V1 animation/impact polish

kicks / kick chains / later Ultras

rifle left-hand IK after final animations

Amy short-cover aim-point priority

circular target rail

pistol/rifle recoil/audio/reload/mechanical polish

held-item visual-offset authoring cleanup

Fire / Ice / Toxic grenade families

additional grenade families later

LATER / PRODUCTION

mobile profiling: CPU/GPU/draw calls/overdraw/VFX/physics/memory/thermal

projectile/VFX pooling where justified

grenade automatic Blender → Unity LOD pipeline

Low / Medium / High graphics presets

Alien Beam / Hoist

Gauntlets

objective presentation

dialogue/subtitles

final pause/settings UX

achievements / leaderboards if useful

analytics

loading-background safe framing

final app icon

PERMANENT RULE — Gameplay Scene Setup

Canonical command:

Tools → Kids VS Aliens → Setup → Setup or Repair Active Gameplay Scene

Whenever a new standard gameplay/player dependency or required wiring is introduced,
extend this helper in the same task. Keep it idempotent and safe to rerun.
