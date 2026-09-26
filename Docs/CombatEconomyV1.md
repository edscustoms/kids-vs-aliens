# Combat Economy V1

26 September 2026. Initial configurable economy, not final balance.

## Ownership and behavior

- `PlayerInventory` owns Plasma/Armor Capsule counts and each `OwnedWeaponState`. Capsules
  use no backpack or quick slot. Fresh runs start with zero capsules and full newly owned
  magazines; no starting resource grant was added. Dropping a player-owned gun transfers
  its existing state to that specific `PickupItem`; recollection transfers it back, without
  creating a fresh magazine. Its world snapshot preserves rounds and remaining timers.
- `PlayerEquipment` still selects/mounts; `PlayerShooter` operates the selected state.
  Empty, usable plasma guns auto-reload when resources are sufficient, including after
  collecting capsules while empty. Payment occurs once at reload start; a paid reload
  continues on scaled gameplay time while holstered or dropped. Pause freezes it. The
  reload transaction rejects inactive weapons; gaining Plasma cannot start their reloads.
  There is no manual
  partial reload, proportional price or reload-target UI. Fire cooldowns/swaps/reselection
  and approved delayed plasma damage remain intact.
- `PickupItem` deposits CapsuleItemData quantities or converts an already-owned plasma
  weapon using weapon-authored reward data, then marks its world identity removed once.
  Ordinary inventory insertion still enforces uniqueness; EnsureOwnedWeapon and restore
  cannot award conversion resources. Plasma/Armor pickups and conversions work with a full
  backpack. Zero duplicate reward disables conversion and leaves the duplicate in the
  world; nonplasma duplicate rules remain unchanged. Existing feedback shows short
  rewards; nearby same-resource rewards aggregate.
- `EnemyPlasmaLoot` owns one configurable death roll. `EnemyEquipment.OnDeath` calls it with
  the live weapon before equipment drops/returns it. A weapon flagged as plasma is required;
  unarmed/nonplasma aliens do not receive automatic plasma loot. The reference alien uses
  75% chance and 2–4 capsules. Equipment custody remains separate: the actual scavenged
  world gun returns once, alongside independently generated loot. Enemy reloads retain
  their existing behavior; the player Capsule pool is not an enemy resource pool.
- Armor Capsules have independent quantity, green pickup family and HUD count. They do
  not restore armor yet: the consumption interaction/amount remains a design decision.

## Starting weapon data

| Value | Pistol | Rifle |
|---|---:|---:|
| Magazine | 12 | 28 |
| Plasma cost / full reload | 3 | 6 |
| Reload duration (unchanged) | 0.8 s | 1.6 s |
| Duplicate pickup reward | 6 | 12 |
| Damage / round | 18 | 8 |
| Fire rate limit | 3/s, semi-auto | 18/s, automatic |
| Range (unchanged) | 15 m | 25 m |

WeaponItemData owns these values and the plasma flag/dry-fire SoundEvent. The shared
weapon data also supplies armed aliens' weapon stats. Pistol damage per capsule is higher;
rifle damage per second is higher. This does not certify final TTK, encounters or difficulty.

## Authoring and presentation

Edit `Assets/Game/Items/Weapons/PlasmaPistolItem.asset` / `PlasmaRifleItem.asset`, and
EnemyPlasmaLoot on the reference enemy prefab or authored instances. Future plasma guns
configure the same weapon fields; there are no weapon-name branches in gameplay.

Resource data lives in `Assets/Game/Items/Resources`; world prefabs in
`Assets/Game/Prefabs/Items/Resources`. PickupItem.quantity controls units per pickup and
persists for generated drops. Place capsule centers above the floor; enemy drops start
0.55 m above their root. Run canonical scene repair after placing persistent content.
No level pickup placement or scene was changed in this pass.

Temporary capsule prefabs use shared URP Lit materials, translucent shells, emissive cores,
small bands and existing WorldItemFloat/PickupGlowPulse. Plasma is cyan/blue; Armor is green.
There are no runtime material copies, realtime lights or new shaders. Final models remain
deferred. CombatEconomySetup creates missing dependencies and repairs missing enemy loot
references through canonical repair; it preserves existing tuning and adds no Tools menu.

GameplayInterface builds CombatAmmoDisplay beneath the existing SafeArea. It observes the
selected inventory entry, displays current/capacity, both resource counts, RELOADING plus
a compact progress line, and NEED PLASMA for empty/insufficient guns. It never intercepts
pointer input; quick-slot and action ownership remain unchanged.

Both weapons reference `Weapon_Plasma_DryFire.asset`, registered in AudioLibrary. It uses
a short synthesized mono trigger placeholder, replaceable through SoundEvent variants.
An empty/insufficient gun requests one semantic click per trigger hold, with no shot,
fire haptic or fire-camera feedback. Gameplay contains no direct clip playback.

## Persistence

Active Run stores both resource quantities absolutely, all owned magazines/timers and the
existing selection. Pickup quantities and resolved loot flags are small IRunStateParticipant
records. Restoration applies twice without awarding pickups, rerolling loot or charging
reloads. Removed world identities retain consumed/conversion results. No released-save
migration framework was added. Old missing resource fields default to zero; old magazine
fallback stays intact and capacities clamp to current data. A legacy pending reload is
honored without a new charge. Permanent Progress remains separate.

## Validation

Commands and isolation follow [Tests README](../Assets/Game/Tests/README.md).
Unity 6000.5.6f1; physical disposable project copy, graphics enabled, isolated saves/PlayerPrefs.

- Final runtime + Editor Roslyn compilation passed (`Logs/CombatEconomy/compile-final.log`).
  Existing unused-field/obsolete Unity API warnings remain; no new compiler warning was introduced.
- Focused regression: **57/57 passed, 0 failed, 0 skipped** in
  `Logs/RepositoryAuditRemediation/CombatEconomy-Focused-04-tests.xml` and matching `-unity.log`.
  Covers economy, actual Save/Continue/HUD, owned weapon state, inventory/pickups, scavenged
  weapon custody, audio, haptics, procedural UI and capsule rendering. Fifteen new cases.
- Audio/import correction verification: **9/9 passed, 0 failed, 0 skipped** in
  `Logs/RepositoryAuditRemediation/CombatEconomy-AudioImport-05-tests.xml` and matching log.
  Includes UIAudioTests, CombatEconomyPlayTests and CombatEconomyAuthoringTests.
- Final full graphics-enabled Editor regression: **333/333 passed, 0 failed, 0 skipped**.
  `Logs/RepositoryAuditRemediation/CombatEconomy-Full-03-tests.xml` and matching `-unity.log`;
  Unity exited 0. This includes all 318 baseline cases plus 15 new cases.
  No known automated test failures remain.
- `git diff --check` passed. All six existing serialized assets were inspected; their
  changes are limited to the two weapon data assets, reference enemy loot component,
  AudioLibrary, feedback catalog and RunContentCatalog. New materials/prefabs/resource
  data/dry audio/import metadata were inspected; all new GUIDs are unique and asset metas
  are present. No scene, ProjectSettings, package or existing importer/meta was changed.
- Capsule render and HUD 1920x1080 / 1280x720 captures were visually inspected. Captures live
  under `Logs/RepositoryAuditRemediation/TestProject/Logs/CombatEconomy/capsules.png` and
  `.../Logs/ProceduralUI/combat-economy-hud-{1080,720}.png`. These verify Editor presentation,
  not device cost, touch feel, final audio quality or final balance.

Failures found and corrected during development: the first compile of the new custody
assertions lacked `System.Linq`; an early render check caught new capsule materials with
URP's non-emissive GI flag (fixed in assets and creation helper); the following render test
used an unconfigured preview-scene camera (fixed to use the established PreviewRenderUtility
rendering approach). Focused runs 02 and 03 each reported 56/57; final focused run 04 is green.
These were task-introduced code/asset/fixture issues, not pre-existing production failures.
The first Full run additionally exposed `ArmedDeathDropsExactlyOneNormalPickup` counting
all pickups as guns. This assertion was stale under the explicitly changed loot rule:
it now guarantees a capsule roll and independently checks one gun and one Plasma pickup,
even after repeated lethal hits. Full-01 was stopped to apply that fixture correction
(no completed XML; partial results remain in its Unity log), then Full-02 restarted the
complete suite. No production behavior was changed to satisfy that obsolete count.
Full-02 completed **332/333, 1 failed, 0 skipped**: the existing audio validation test
correctly rejected the new dry-trigger clip's disabled preload flag. This was a task-caused
asset import defect; its new `.meta` now requests synchronous preloading. No existing clip
import settings were altered. Final audio verification and the subsequent green Full run are
recorded above.

## Manual state bug follow-up (26 September 2026)

Only dropped-weapon state transfer and the active-weapon reload restriction were changed.
Balance, HUD, loot configuration, scenes and assets remain as in the original V1 pass.

- Confirmed root cause: `DropItem` removed the owned record and spawned a pickup with no
  state. Collection then initialized a full magazine. Inventory now transfers its existing
  `OwnedWeaponState` to the pickup and back before inventory-change observers run. There
  is no second magazine implementation. The existing pickup participant stores an optional
  `SavedWeaponState`; two-pass restore is absolute and does not charge for a pending reload.
- Fresh authored weapons and legacy pickup records without carried state initialize
  normally. They do not inherit state from a different dropped gun of the same definition.
  Old saves that never recorded a dropped magazine cannot recover those missing rounds.
- The old reload transaction accepted inactive owned weapons. It now checks equipment's
  current inventory selection before spending Plasma or starting a timer. Already-paid
  reloads can still finish while holstered or dropped, preserving the existing timing rule.
- Evidence distinction: the pre-fix tests reproduced the drop/refill and inactive API
  acceptance. The acquisition-only scenario already passed against pre-fix GamePoc and
  ConstructionSite; no resource-acquisition fan-out was reproduced. Both scenes now have
  explicit regression coverage for duplicate-rifle and capsule pickups, untouched empty
  pistol state, and subsequent pistol selection/payment.

Verification: focused **39/39 passed**, 0 failed/skipped
(`Logs/RepositoryAuditRemediation/CombatState-Focused-08-tests.xml` and matching Unity log).
All six new state cases pass. Compilation passed (`Logs/CombatStateFix/compile-final.log`),
with no new compiler warnings; `git diff --check` passed. Full Editor regression with
graphics enabled: **339/339 passed**, 0 failed/skipped
(`Logs/RepositoryAuditRemediation/CombatState-Full-09-tests.xml` and matching Unity log).
Baseline evidence is in
`Logs/RepositoryAuditRemediation/CombatState-Baseline-05-tests.xml` (three drop-state
failures plus inactive-reload acceptance, one acquisition-flow pass) and
`CombatState-Baseline-06-tests.xml` (ConstructionSite acquisition flow passed).
Initial fixture failures were corrected: GamePoc has no required Beam controller, learned
weapon tutorials must be acknowledged before testing input, and the isolated EditMode
inventory fixture must wire its equipment reference explicitly. Production suspension/UI
behavior was not changed to make the tests run. The new fresh-pickup snapshot test also
caught Unity materializing absent nested state as a zeroed object (Focused-07: 38/39).
An explicit `carriesWeaponState` marker now distinguishes an actual dropped record from
fresh/legacy pickups; balance and initial magazine values were not changed.

Files changed in this follow-up (the 68-file inventory below records the original V1 pass):

```text
Assets/Game/Scripts/Items/OwnedWeaponState.cs
Assets/Game/Scripts/Items/PickupItem.cs
Assets/Game/Scripts/Player/PlayerInventory.cs
Assets/Game/Tests/Editor/Core/CombatEconomyStateTests.cs
Assets/Game/Tests/Editor/Core/CombatEconomyStateTests.cs.meta
Assets/Game/Tests/Editor/Core/CombatEconomyTests.cs
Assets/Game/Tests/README.md
Docs/CombatEconomyV1.md
Docs/RunInterface.md
```

Retest: leave each gun partly loaded, drop/recollect it, and repeat across Save/Continue.
Also drop during a paid reload and check no second charge. With both guns empty and rifle
equipped, collect a duplicate rifle: 12 Plasma becomes 6, rifle refills, pistol stays empty;
selecting pistol can then spend 3 to reload it.

## Manual Editor acceptance

1. Equip a learned pistol/rifle. Fire through a magazine; collect Plasma and verify the
   correct fixed cost, timed refill and fast rifle cadence. No capsules should disappear
   per bullet. With too few capsules, hold/release/fire and listen for one dry click per hold.
2. Leave pistol at 4/12 and rifle at 13/28; switch/reselect. Check both retain their rounds.
   Switch during a paid reload; verify no second charge and pause freezes its progress.
3. Pick up Plasma/Armor and duplicate guns, including with a full backpack. Check short
   feedback, separate counts, distinct temporary visuals and no second inventory gun.
4. Kill a plasma-equipped alien and an unarmed one. Test scavenging: the original gun
   returns once; a separate probabilistic capsule drop never replaces that gun.
5. Save/Quit/Continue with partial magazines, resources, a paid pending reload and uncollected
   drops. Check counts/selection/HUD, no repeated pickup/conversion/loot reward, and paused return.
6. Inspect HUD at desktop/mobile aspect ratios, reload/empty styling, quick slots and touch
   controls. Check capsule visibility on real ConstructionSite lighting/ground.

Android/iOS feel, rendering and lifecycle/device profiling remain separate. No Difficulty,
final balance, upgrades, shops, armor consumption or new level content was implemented.

## Changed files

68 files: 28 modified, 40 added (including 21 new `.meta` files). No deletions.

Modified:

```text
AGENTS.md
Assets/Game/Audio/AudioLibrary.asset
Assets/Game/Data/Presentation/GameplayFeedbackCatalog.asset
Assets/Game/Editor/Enemy/EnemyCombatantSetup.cs
Assets/Game/Editor/GameplaySceneSetup.cs
Assets/Game/Items/Weapons/PlasmaPistolItem.asset
Assets/Game/Items/Weapons/PlasmaRifleItem.asset
Assets/Game/Prefabs/Enemies/PF_Enemy_Melee_POC_V1.prefab
Assets/Game/Resources/RunContentCatalog.asset
Assets/Game/Scripts/Enemy/EnemyEquipment.cs
Assets/Game/Scripts/Feedback/FeedbackPresentationCatalog.cs
Assets/Game/Scripts/Feedback/FeedbackScheduler.cs
Assets/Game/Scripts/Feedback/GameplayFeedbackEvent.cs
Assets/Game/Scripts/Items/PickupItem.cs
Assets/Game/Scripts/Items/WeaponItemData.cs
Assets/Game/Scripts/Persistence/ActiveRunController.cs
Assets/Game/Scripts/Persistence/RunSaveData.cs
Assets/Game/Scripts/Player/PlayerInventory.cs
Assets/Game/Scripts/Player/PlayerShooter.cs
Assets/Game/Tests/Editor/Core/AlienCombatantTests.cs
Assets/Game/Tests/Editor/Core/AuditContinueTests.cs
Assets/Game/Tests/Editor/Core/InventoryStackTests.cs
Assets/Game/Tests/README.md
Assets/Game/UI/Screens/GameplayInterface.cs
Docs/ProceduralUI.md
Docs/RunInterface.md
PROJECT_CONTEXT.md
TODO.md
```

Added:

```text
Assets/Game/Audio/Clips/Weapons/plasma-dry-trigger.wav
Assets/Game/Audio/Clips/Weapons/plasma-dry-trigger.wav.meta
Assets/Game/Audio/Events/Weapons/Weapon_Plasma_DryFire.asset
Assets/Game/Audio/Events/Weapons/Weapon_Plasma_DryFire.asset.meta
Assets/Game/Editor/Helpers/CombatEconomySetup.cs
Assets/Game/Editor/Helpers/CombatEconomySetup.cs.meta
Assets/Game/Items/Resources.meta
Assets/Game/Items/Resources/ArmorCapsule.asset
Assets/Game/Items/Resources/ArmorCapsule.asset.meta
Assets/Game/Items/Resources/PlasmaCapsule.asset
Assets/Game/Items/Resources/PlasmaCapsule.asset.meta
Assets/Game/Materials/Capsules.meta
Assets/Game/Materials/Capsules/ArmorBand.mat
Assets/Game/Materials/Capsules/ArmorBand.mat.meta
Assets/Game/Materials/Capsules/ArmorCore.mat
Assets/Game/Materials/Capsules/ArmorCore.mat.meta
Assets/Game/Materials/Capsules/ArmorShell.mat
Assets/Game/Materials/Capsules/ArmorShell.mat.meta
Assets/Game/Materials/Capsules/PlasmaBand.mat
Assets/Game/Materials/Capsules/PlasmaBand.mat.meta
Assets/Game/Materials/Capsules/PlasmaCore.mat
Assets/Game/Materials/Capsules/PlasmaCore.mat.meta
Assets/Game/Materials/Capsules/PlasmaShell.mat
Assets/Game/Materials/Capsules/PlasmaShell.mat.meta
Assets/Game/Prefabs/Items/Resources.meta
Assets/Game/Prefabs/Items/Resources/ArmorCapsule.prefab
Assets/Game/Prefabs/Items/Resources/ArmorCapsule.prefab.meta
Assets/Game/Prefabs/Items/Resources/PlasmaCapsule.prefab
Assets/Game/Prefabs/Items/Resources/PlasmaCapsule.prefab.meta
Assets/Game/Scripts/Enemy/EnemyPlasmaLoot.cs
Assets/Game/Scripts/Enemy/EnemyPlasmaLoot.cs.meta
Assets/Game/Scripts/Items/CapsuleItemData.cs
Assets/Game/Scripts/Items/CapsuleItemData.cs.meta
Assets/Game/Tests/Editor/Core/CombatEconomyAuthoringTests.cs
Assets/Game/Tests/Editor/Core/CombatEconomyAuthoringTests.cs.meta
Assets/Game/Tests/Editor/Core/CombatEconomyTests.cs
Assets/Game/Tests/Editor/Core/CombatEconomyTests.cs.meta
Assets/Game/UI/Screens/CombatAmmoDisplay.cs
Assets/Game/UI/Screens/CombatAmmoDisplay.cs.meta
Docs/CombatEconomyV1.md
```
