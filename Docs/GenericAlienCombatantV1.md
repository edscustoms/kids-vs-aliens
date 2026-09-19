# Generic Alien Combatant V1

> Follow-up: [Weapon alignment and ranged refinement](WeaponAlignmentRefinement.md) supersedes the initial socket ownership, three-shot cadence and gate drop defaults described below.

## Existing foundation and scene scope

The reference is still `Assets/Game/Prefabs/Enemies/PF_Enemy_Melee_POC_V1.prefab`. There is no second enemy prefab/framework. EnemyActor, health, NavMesh motor, perception/FOV/LOS, approach slots, wandering, directional investigation, precise animated melee contact, reaction locks, stun and death remain in place.

The prefab's **Starting Weapon is None**, and **Allow Weapon Pickup is false**. The new capability therefore does not automatically arm existing encounters. ConstructionSite has exactly two explicitly authorized overrides next to the north-east locked gate:

| Instance | Position before runtime navigation | Starting weapon |
| --- | --- | --- |
| PF_Enemy_Melee_POC_V1 (7) | (29.34, 0.20, 37.88) | Plasma Pistol |
| PF_Enemy_Melee_POC_V1 (8) | (27.49, 0.20, 37.41) | Plasma Rifle |

The scene change consists of two serialized weapon references. No enemy positions, level geometry, player weapons, Knowledge, inventory, UI, input, camera or Beam Hoist behavior were changed. GamePoc remains unarmed.

## Runtime responsibilities

* **EnemyEquipment** owns one WeaponItemData, attached WeaponInstance, muzzle and magazine. It supports explicit equip, unequip and optional drop-on-death. It does not own a backpack, player skill state, quick slots or input.
* **EnemyCombatProfile** supplies compatible weapon definitions, melee/ranged capability flags, aim delay, spread, range, hysteresis, automatic burst size/pause and local pickup limits. Existing health, navigation, perception, melee and investigation tuning stays editable on the original components.
* **EnemyRangedAttack** handles aim readiness, weapon cadence, magazines/reload, physical fire queries and small local repositioning. EnemyBrain only asks it whether ranged combat owns the current decision.
* **EnemyWeaponAwareness** optionally scans available pickups at a bounded interval. It checks distance, compatibility, visibility and a complete NavMesh path before reserving one. It only seeks a weapon while unarmed. A visible immediate threat, hit/stun lock, disable, timeout, loss of availability or death cancels the reservation.
* **EnemyCombatPresentation** selects the authored CharacterVisual and animation package. Optional per-style sockets belong to the visual, not to the shared weapon definition.
* **EnemyAnimationProfile** supplies a replaceable controller and ranged semantic readiness/fire mappings. Existing melee/hit/death components retain their serialized semantic contracts. AnimatorOverrideController can replace motions without changing EnemyBrain.
* **WeaponShotQuery** supplies nearest physical blocker checks, excluding the shooter and explicit ProjectilePassThroughObstacle objects. Full non-alloc buffers use a rare complete-query fallback rather than silently omitting cover.

PickupItem now exposes local availability, reservation and consumption. Its existing player pickup path keeps priority and retains its inventory/Knowledge checks. Collection immediately removes availability and marks the world object removed before deferred destruction. Competing aliens cannot both own the same pickup.

## Combat decisions

Visible target, no weapon: original melee approach/contact logic. A locally attractive weapon can take priority only when pickup is explicitly allowed and the visible target is outside the immediate-threat distance.

Visible target, armed: approach a preferred distance capped by the actual weapon range, stop, face, settle into the correct weapon stance, then fire. A blocked muzzle prompts a small NavMesh sidestep. When perception loses sight, the existing last-known-position investigation takes over; ranged logic does not track a hidden target through walls.

Close target: enter melee at 1.05 m. The gun visual hides while unarmed melee is used; ownership and ammo remain intact. Resume ranged only beyond 1.8 m and after a committed melee animation finishes. Each original melee strike still resolves independently through the authored contact marker and animated limb/physics validation.

Default ranged tuning: 0.65 s acquisition/settle delay, 2.5-degree disk spread, 6 m preferred range, 12-degree root-facing tolerance, three-shot automatic bursts, 0.7 s burst pause. WeaponItemData still supplies damage, maximum range, fire rate, magazine size, reload time, fire mode, equipped/world prefabs and animation style. Rifle intra-burst shots follow its 15 Hz rate; pistol is single-shot with the configured pause. No player required-skill gate is consulted.

Every shot rechecks the animated muzzle after animation evaluation, current action readiness, target validity, facing, world obstruction and range. A badly misaligned barrel cannot fire. Physical hitscan contact applies damage at emission through CombatHitResolver/HitInfo/IDamageable; the existing pooled plasma bolt, muzzle and impact are visual feedback. This is a hitscan V1, not a new travelling-ballistic simulation. The visual bolt does not schedule future damage against a remembered target.

Hit/stun/death/disable cancels pending aim, reload and burst requests. No delayed damage callback survives interruption. An already-resolved physical shot is not retroactively cancelled. Existing precise melee cancellation remains unchanged.

## Equipment and persistence

The existing GripAttachmentUtility and WeaponInstance attach the same pistol/rifle assets used by the player. The reference alien needed authored socket calibration, particularly for the rifle: its initial barrel was approximately 93 degrees sideways and correctly failed the firing guard. Per-style sockets now correct that visual package without editing the player's weapon prefabs or using a per-frame rotation workaround. The rifle's off-hand IK remains untouched.

EnemyEquipment implements the existing IRunStateParticipant extension with key `enemy-equipment-v1`, saving stable catalog weapon identity and magazine count. Pickup removal and optional spawned drops use existing RunWorldObject tracking. No active-run schema, lifecycle or inventory architecture changed. Saves without an equipment part retain authored starting behavior. Never-activated encounter children capture their authored loadout without creating an equipment visual during capture. Inactive restore defers Animator parameter application until initialization. Reload progress, transient projectiles and animation frames are intentionally not persisted.

V1 reload replenishes the magazine after WeaponItemData.reloadTime. There is no finite reserve-ammo/scavenging economy. Optional drops use the existing normal world pickup definition; partial enemy magazines are not transferred into player ammunition.

## Designer workflow

1. Place the existing enemy prefab or create a prefab variant. Leave Starting Weapon=None for unarmed, or assign a compatible WeaponItemData on EnemyEquipment.
2. Enable EnemyWeaponAwareness > Allow Weapon Pickup only for encounters intended to scavenge. Its profile must also allow pickup and the desired weapon definition.
3. Assign/duplicate AlienCombatV1 for ranged/pickup variation. Continue tuning the original modular components for health, locomotion, perception, investigation and melee.
4. For a new model, import a valid Humanoid Avatar and place its visual prefab as the enemy root's visual child. Remove the old visual child. Keep gameplay, body collider/NavMeshAgent and AimTarget on the combatant root.
5. Give the visual a CharacterVisual referencing its Animator and a right-hand weapon socket. The equipped weapon supplies GripPoint and Muzzle. Assign that CharacterVisual on EnemyCombatPresentation. Optional Weapon Mounts provide per-style sockets for that rig; remove references to the old visual's mounts.
6. Assign EnemyAnimationProfile with a compatible controller/override controller. Keep MoveX/MoveY, WeaponStyle, melee/hit/death contracts consistent with their components, or update their serialized mappings. The melee clip needs its typed MeleeImpact event; its Hit state needs EnemyHitStateLockBehaviour. Tune the existing MeleeContactShape bone/radius for the new anatomy rather than adding a large invisible reach volume.
7. Run **Tools > Helpers > Repair Selected Alien Combatant**. It fills missing capability references and reconnects the existing animation consumers to the assigned visual. It does not replace an assigned profile, starting weapon, pickup choice or tuned socket transform.
8. Inspect grip/muzzle alignment in Unity with both aim styles, then validate melee contact, hit/stun cancellation and death on the new rig. No per-alien AI class is needed.

The canonical **Tools > Setup > Setup or Repair Active Gameplay Scene** and existing Enemy POC setup call the same feature helper. Normal repair never assigns the gate loadouts or recalibrates authored sockets. The one-time gate/calibration batch authoring methods are separate from routine repair.

## Validation and limitations

Unity batch compilation, isolated Play Mode encounter tests and captured render inspection were used. No Android/device validation was performed. Evidence is in `Logs/AlienCombat`; the existing full melee matrix remains in `Logs/CombatPrecision/refined`.

Validation covers authored pistol/rifle spawn, real shot damage and different cadence, close-range switching/hysteresis, cover, hit/stun/disable, two-alien pickup competition, immediate player pressure, occluded pickups, equipment round trips including inactive actors, mixed three-alien ranged/melee pressure, and a Granny Humanoid replacing the temporary alien visual. Repair is checked twice for duplicate components and preservation of authored loadout/pickup/socket edits.

Final results (19 September 2026):

| Evidence | Result |
| --- | --- |
| `Logs/AlienCombat/edge-final.xml` | 9/9: six Play Mode encounter/persistence tests plus three authoring checks |
| `Logs/AlienCombat/scene-repair.xml` | 5/5 authoring checks, including repeated shared repair on ConstructionSite and GamePoc |
| `Logs/AlienCombat/reservation-final.xml` | 1/1 targeted rerun: an unseen incoming hit releases a reserved weapon immediately |
| Existing regression fixtures in `Logs/AlienCombat/final.xml` | 17/17: damage resolution, movement locks, persistence and the full refined melee encounter matrix |

The broad `final.xml` run originally had an authoring-test teardown failure because batch Unity had no initial scene to restore; the corrected fixture passed in the later runs above. The six new encounter tests and five authoring tests are 11 distinct checks, alongside the 17 existing regression checks. The melee matrix includes 55 player strike cases, 36 enemy strike cases, combo/target changes and nine complete 1/2/3-alien encounters.

Observed low-cover trial: the rifle alien moved 0.75 m sideways and then fired six shots from its clear position. Mixed encounter: pistol/rifle shots and two valid unarmed melee contacts coexisted. Pistol/rifle aim, melee fallback, pickup, mixed encounter, alternate visual and low-cover captures were inspected. The prior project's MobileAimSettings total warning and nonexistent `Hit` trigger warning in death tests remain unrelated known warnings; they were not suppressed.

Short designer smoke check: start the level and inspect the two gate loadouts; test an opt-in unarmed alien with a nearby dropped gun; approach/retreat to check melee/ranged switching; interrupt before firing; save/continue after pickup; rerun scene repair and confirm authored loadouts stay intact.

Remaining deliberate V1 limits: local sidestepping/investigation rather than tactical cover selection; no weapon upgrades while already armed; no reserve-ammo economy; no independent upper-body aim IK, dedicated reload/pickup choreography or recoil clip in the reference profile (aim stance and plasma muzzle flash provide the current presentation); optional authored fire trigger is available for a future compatible animation set. Alternate models still need socket/contact calibration and visual review. Automated correctness and inspected captures do not establish final encounter balance or animation polish.

## Changed files

New runtime components/data: `Scripts/Enemy/EnemyCombatProfile.cs`, `EnemyAnimationProfile.cs`, `EnemyCombatPresentation.cs`, `EnemyEquipment.cs`, `Scripts/Enemy/AI/EnemyRangedAttack.cs`, `EnemyWeaponAwareness.cs`, and `Scripts/Combat/WeaponShotQuery.cs` (all below `Assets/Game`).

Existing runtime edits: `EnemyBrain.cs` adds capability delegation and appended broad state values; `EnemyLocomotionAnimator.cs`, `EnemyMeleeAttack.cs`, `EnemyHitReaction.cs` and `EnemyDeathSequence.cs` resolve the assigned visual Animator; `PickupItem.cs` shares availability/reservation/consumption with enemy pickup while retaining the player path.

Authoring: new `Assets/Game/Editor/Enemy/EnemyCombatantSetup.cs`; calls from `EnemyPocSetupWindow.cs` and `GameplaySceneSetup.cs`; new `Assets/Game/Data/Enemies/AlienCombatV1.asset` and `AlienAnimationV1.asset`; existing `HumanoidMeleeEnemy.controller` adds pistol/rifle locomotion and weapon-style transitions; the existing reference prefab adds the capabilities, CharacterVisual/relay wiring and calibrated socket children; `ConstructionSite.unity` adds only the two weapon overrides.

Tests: `Assets/Game/Tests/Editor/Core/AlienCombatantTests.cs` and `AlienAuthoringTests.cs`. Unity `.meta` files accompany new scripts, assets and folders. This document contains the authoring/validation report.
