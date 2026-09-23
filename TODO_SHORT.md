Kids VS Aliens — Short TODO

NOW — Next ~2 Weeks

CURRENT STATUS UPDATE — 18 Sep 2026

Camera Occlusion Fade V2 — DONE WITH EXTRA TODO

Core V2 is working and production-sensitive. Keep real-level/device regression checks and
small readability tuning, but do not treat it as an open architecture rewrite.

Alien Beam / Hoist V1 — DONE WITH EXTRA TODO

Manual upward Beam Hoist now uses normal Jump, one continuous cubic Bézier and generic
BeamHoistSurface + BeamHoistSurfaceBaker authoring.

BeamHoistAbility limits are the bake source of truth. New surfaces in configured gameplay
scenes automatically bake the fixed valid lower approach/start area.

The manual hoist-start hologram is working:

Knowledge gated

proximity fade in/out with Reveal/Hide hysteresis

exact baked allowed area

fixed position/shape at runtime

current idle glow is approved

current active flicker/power-up is approved

hides when the real hoist starts

disconnected valid pockets remain separate

contiguous valid regions should visually merge without filling invalid gaps

Extra TODO:

keep the visually preferred/original Floating import behavior; setup must not silently
force an importer configuration that changes the motion

finish/verify connected-region visual merge if the current pass is still open

Beam Hoist tutorial demonstration

later BeamTransportVFX spark polish only

fix automatic LevelStart/BeamInSpawn placement + lingering-beam issue independently

investigate the pre-existing desktop Space/Jump lock independently

Android/device regression

Knowledge + Feedback + Pause + Tutorial Presentation V1 — DONE WITH EXTRA TODO

Framework is working. Still add/audit learned-action demonstrations for Grenade and
Beam Hoist and later fold final tutorial/control visuals into the HUD redesign.

Camera Occlusion Fade V2 — ABSOLUTE MUST

Move from renderer-centric fading to logical CameraFadeOccluder groups.

Keep exact camera → player rays; use ~5 body samples.

Fade whole logical blockers smoothly with short hysteresis.

Keep colliders/gameplay active.

Preserve PlayerAim blocker semantics.

Use MaterialPropertyBlock; no per-renderer material instances.

Add an idempotent level-authoring helper for occluder setup/validation.

Test against real construction-site geometry.

Construction Site + Replay Test — ABSOLUTE MUST

Initial route:

START / Amy wakes after being beamed into the Construction Site
→ unfinished construction building
→ crash zone

That is the only initial route.

Core Level 1 goal:

GET OUT OF THE CONSTRUCTION SITE / FIND A WAY TOWARD SCHOOL.

All playable Level 1 content remains on the Construction Site for now.
No school gameplay/interactions yet.

Then build/test:

crash zone
→ wider Construction Site encounters / traversal
→ short site-specific objectives every ~1–3 minutes
→ excavator escape set-piece
→ continue toward the Construction Site exit / direction of school

First/early successful-run target:

~12–20 minutes

Mastered-run target:

~6–10 minutes

The level should keep changing the player's immediate task so the first run stays interesting:

traversal

combat

finding a useful item / weapon

short retrieval objectives

environmental interaction

new functional Construction Site zones

replay it ~5–10 times

Attempt 5 should feel faster/smarter than Attempt 1

if replay feels like chores, fix the loop before expanding the full level

MUST-HAVE EARLY SET-PIECE — BREAK YOUR WAY OUT WITH THE EXCAVATOR

A Construction Site exit/route is physically blocked by stacked concrete roadblocks/barriers.

Do not solve this specific escape beat with a random key/keycard.

Amy must repair/activate the excavator and physically clear the obstruction.

Repair objective:

REPAIR EXCAVATOR — 0/3

Battery cables / jumper leads

Hydraulic fluid

Fuse

Place each required item in a believable Construction Site zone, for example:

battery cables → maintenance / electrical / workshop area

hydraulic fluid → machinery service / fuel area

fuse → site office / electrical storage area

V1 interaction stays intentionally simple:

find required item
→ pick it up
→ bring it to the excavator repair/drop-off zone
→ matching repair step auto-completes
→ show simple status text

Example:

Battery cables installed — 1/3

Hydraulic fluid added — 2/3

Fuse replaced — 3/3

No bespoke repair animations required for V1.

After 3/3:

activate excavator

excavator base may stay static

arm/bucket uses simple authored rotation/translation

arm/bucket physically pushes/topples the stacked concrete roadblocks with Rigidbody physics

do not build a complex real-time wall-fracture/destruction system

add a deterministic exit-clear check/fail-safe so physics can never soft-lock progression

Treat this excavator escape as a near-term must-have Level 1 gameplay beat, not distant polish.

ABSOLUTE MUSTS — Core Game

Large handcrafted story levels; do not turn the campaign into procedural-room runs.

Working death rule: death restarts the current large story level.

Mobile interruption/app background must never equal death.

Safe active-run suspend/resume before release.

ACTIVE-RUN SUSPEND / RESUME CLARIFICATION — ABSOLUTE MUST

Incoming calls, backgrounding, phone lock, normal manual quit, app close/reopen and
recoverable OS termination must preserve the current attempt.

Continue on relaunch must restore the same functional run/place/state.

Only:

death

explicit Hard Restart from the in-game menu (with confirmation)

may intentionally discard the active-run state and restart the current large story level.

Ordinary Quit must preserve the run.

Keep permanent/meta progression separate from active-run persistence. The active run must
be able to restore Amy position/state, objectives, mission items, changed interactables,
meaningful enemy/encounter state and set-piece progress such as excavator repair state.

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

DONE WITH EXTRA TODO

See the 18 Sep current-status block above. Core manual upward hoist, generic surface bake,
fixed discovery VFX and Floating presentation are implemented; only the listed
presentation/tutorial/LevelStart/jump/device follow-ups remain.

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

Tools → Setup → Setup or Repair Active Gameplay Scene

Whenever a new standard gameplay/player dependency or required wiring is introduced,
extend this helper in the same task. Keep it idempotent and safe to rerun.
