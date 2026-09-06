Kids VS Aliens — Short TODO

PERMANENT RULE — Gameplay Scene Setup

Tools → Kids VS Aliens → Setup → Setup or Repair Active Gameplay Scene is the canonical gameplay-scene repair/setup path. Whenever a new standard player/scene dependency or required wiring is added, extend this helper in the same task. Keep it idempotent and safe to rerun; level designers should not need to remember repeated Inspector wiring.

Grenade Gameplay + Electric VFX: Done

Reusable grenade gameplay is working, and Electric Grenade VFX V1 is closed until the final game V1 polish pass.

Grenade Throw Animation V1: Done

Semantic action/event integration, charge preservation, authored release timing, fallback/interruption handling and shared Amy/Granny wiring are working. Current motion is acceptable for V1; final animation polish is parked.

Knowledge + Feedback + Pause + Tutorial Presentation V1: Done

Knowledge tutorial/popup, feedback and pause foundations work. Standard gameplay scenes can be repaired through the central Gameplay Scene Setup helper. Further UI/device polish can happen later.

Plasma Pistol: Done

Core pistol gameplay, VFX, pickup/equip flow, muzzle safety and Knowledge gating are working.

Plasma Rifle: Done

Core rifle gameplay, assets, automatic fire, menu integration and Knowledge gating are working.

Melee Alien V1: Done

Navigation, perception, chase, investigation, melee attack, hit reaction and death are working.

Core Mobile Foundation: Done

Android build, mobile controls, auto-aim, target switching, LOS/cover and wall fading are working.

Unarmed Combat V0 Foundation: Done

Fighting Knowledge/item flow, plain-unarmed FIRE attacks, ~2m automatic combat stance, ~3s inactivity exit, semantic punch actions, authored MeleeImpact damage timing and real-enemy damage/death are working. Current Amy combat animations are placeholder-quality.

Generic Melee Weapon Framework: TODO

Create reusable melee architecture for bats, pipes, crowbars, sticks, alien blades and similar weapons, reusing the proven combat/animation-event foundations.

Ranged Alien V1: TODO

Add a reusable ranged enemy role that reuses the existing enemy perception/navigation foundation.

Unarmed Combat V1 Polish: TODO / Parked

Replace FightingIdle/Boxing placeholder animations, improve blending and impact feel, then add kicks/kick chains and later combo/Ultra/proficiency polish without rewriting the V0 gameplay flow.

Rifle Left-Hand Grip / IK: TODO

Add left-hand rifle IK after final rifle animations exist.

Amy Short-Cover Aim Priority: TODO

Prefer clear torso/upper-body aim points when low cover blocks the normal shot path.

Circular Target Rail: TODO

Add circular movement support to the existing target rail system.

Camera Feel Pass: TODO

Compare current framing, a slightly closer camera and subtle aim/movement look-ahead in real combat.

Pistol / Rifle Polish: TODO

Add recoil, audio, reloads, mechanical animation, battery/eject behavior and charged pistol polish.

Held-Item Attachment Authoring Cleanup: TODO / Parked

Keep the current GripPoint system for V1. Later attach held-item roots 1:1 to WeaponSocket and use child visual offsets so per-item pose tuning is straightforward and does not require inverse GripPoint calculations.

Mobile Profiling: TODO

Profile CPU, GPU, draw calls, overdraw, VFX, physics, memory and thermal behavior before optimizing.

VFX / Projectile Pooling: TODO

Pool frequent effects such as plasma bolts, muzzle/impact VFX, grenade VFX and enemy projectiles where justified.

Grenade LOD / Mobile Rendering Pipeline: TODO

Build one automatic Blender → Unity LOD workflow for shared and unique grenade meshes, materials and LODGroups.

Graphics Presets: TODO

Create meaningful Low, Medium and High presets that change real rendering/VFX costs.

Android / Build Maintenance: TODO

Keep baked target colliders, avoid unnecessary FBX Read/Write and use Android Logcat for device debugging.

Fire Grenade VFX: TODO

Create the Fire grenade effect family using the existing grenade foundation.

Ice Grenade VFX: TODO

Create the Ice grenade effect family using the existing grenade foundation.

Toxic Grenade VFX: TODO

Create the Toxic grenade effect family using the existing grenade foundation.

Additional Grenade Families: TODO

Later consider Gravity/Pull, Healing, Sticky, Smoke/Confusion, EMP and other variants.

Level / Content Support Systems: TODO

Support level construction with ranged enemies, grenades, melee, objectives, dialogue and reusable encounter tools.

Alien Beam / Hoist: TODO

Create a reusable alien capture/lift/vertical transport/release traversal prefab.

Gauntlets: TODO

Build the future gauntlet combat family with combos, powered attacks, charged moves and Ultras.

Progression Expansion: TODO

Keep Knowledge permanent while proficiency primarily resets per story level/run and unlocks meaningful abilities.

Objective Presentation: TODO

Add clear in-game objective presentation for story missions.

Dialogue / Subtitles: TODO

Add reusable dialogue and subtitle presentation for gameplay/story scenes.

Pause / Settings: TODO

Finish player-facing pause and settings screens beyond the current pause foundation.

Achievements / Leaderboards: TODO

Add achievements and leaderboard support later if useful for the released game.

Analytics: TODO

Track important gameplay, progression, completion, quit and performance events.

Loading Background Safe Framing: TODO

Fix loading artwork framing so it survives different mobile aspect ratios without bad cropping.

Final App Icon: TODO

Replace the temporary/default app icon with the final Kids VS Aliens icon.
