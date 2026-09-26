# Interface and active-run persistence

The interface uses the existing uGUI/TMP, inventory, tutorial and suspension systems. Gameplay movement, Beam Hoist paths, camera modes and action bindings retain their existing owners.

## Player flow

- The existing character carousel, preview, background and text-only menu buttons remain. Play starts a fresh run when no snapshot exists; otherwise it opens Active Run Found.
- Continue restores the active run and opens Pause. New Game and Hard Restart require confirmation. Restart restores the original starting loadout, not the current acquired equipment.
- Pause owns one lease across Settings, Inventory and restart confirmation. Inventory Back returns to Pause. Quit to Menu writes a snapshot before leaving and remains in the game if saving fails.
- Learn owns a separate modal lease. Reviewing a learned entry reuses `KnowledgeAcquiredPresenter` and its existing demos. Acknowledgement is permanent; interrupted presentations remain unread.
- The resource HUD displays health and **armor**. The quick bar has five fixed assignments into the real inventory list; it does not add storage or create item copies. Backpack capacity remains the player's authored capacity.

## Save contract

`Application.persistentDataPath/Saves` contains two checksummed generations each of `permanent` and `active-run`. Writes flush a temporary file before replacing the older generation. A torn/corrupt generation falls back to the other valid generation. Confirmed discard writes tombstones to both generations so the recoverable backup cannot resurrect a discarded run.

Permanent data contains skill IDs, total XP and acknowledgement state. Existing camera settings retain their existing permanent PlayerPrefs storage and are flushed on lifecycle transitions. No settings or permanent progression are deleted by run resets.

The active snapshot stores scene, elapsed gameplay time, original loadout, current character, position/facing/vertical velocity, health/armor, inventory, quick assignments, equipment/ammo, scene identities and spawned prefab identities. Pickups, chest loot/open state, resolved enemies, live enemy health/position/awareness, enemy spawn delays and one-shot item spawners participate. Recovered inert-grenade pickups are also tracked.

Restored alert enemies investigate their saved last-known target position; transient attack frames and corpses are not restored. A beam snapshot records a safe endpoint (manual hoist start or automatic arrival destination). Continue explicitly suppresses the fresh-start arrival. It does not reconstruct transient particles, projectiles, animation frames or an in-flight beam.

The default periodic interval is **10 seconds**, configurable on the player's `ActiveRunController`. Important inventory/health/world changes request a debounced snapshot. Background, focus loss, normal quit and Quit to Menu also save. Foregrounding transfers lifecycle suspension to the existing pause menu; it does not resume unattended gameplay. Death invalidates only the active run.

Missing content, incompatible snapshots and restore errors preserve the existing save. A failed restore stays suspended and offers return to menu without overwriting the snapshot.

## Participant and owned-weapon contract (audit remediation)

`PlayerInventory` owns one `OwnedWeaponState` per unique owned weapon definition.
Equipment selects it; the mounted `WeaponInstance` is presentation. Reselection and
pistol/rifle swaps retain rounds, reload deadline and fire cooldown. Reload time continues
on the scaled gameplay clock while stowed; pause freezes it. Active Run saves all owned
magazines plus remaining reload/cooldown times. Legacy snapshots retain selected `ammo`;
missing other weapon records start full. Removing a weapon ends that inventory record;
physical dropped-magazine economy is outside this change. This is pre-release compatibility,
not a released-save migration framework.

`IRunStateParticipant` implementers must read its XML contract. Keys are stable and unique
on each `RunWorldObject`; cross-object references store stable world IDs, never hierarchy
names or scene-instance IDs. Capture describes absolute state. Restore must be idempotent:
never grant rewards, consume resources, replay completion events or invoke incremental
commands merely because a completed state was loaded.

Continue acquires suspension, initializes/registers scene identities, allocates missing
dynamic objects, restores world state, restores the player, waits another frame, then
restores nonremoved world participants **a second time** before declaring readiness.
`OnEnable` can run before saved state is applied. A first restore can see peers' default
state. Defer peer-dependent decisions until `ActiveRunController.IsReady`; resolve them
through `FindWorldObject(stableId)`. Do not depend on participant iteration order. The
`AuditContinueTests` fixture exercises actual Save/Continue with A referencing B, two
restores, no duplicate reward/event and all owned magazines preserved.

## Authoring and extension

Run **Tools → Setup → Setup or Repair Active Gameplay Scene** for gameplay scenes. Run **Tools → UI → Setup or Repair Active Menu** for the menu. `RunInterfaceSetup` is also available as a feature-specific repair. Setup is idempotent and assigns stable identities to relevant authored objects, reuses presentation roots, and generates the GUID-based `RunContentCatalog` resource.

`RunWorldObject.TrackSpawn` registers meaningful runtime spawns with their source prefab. New functional doors, gates and mission/set-piece components implement `IRunStateParticipant` with a stable unique key and JSON state; setup attaches their identity. No objective architecture was introduced. New participant state must be applied without replaying rewards, loot or completion side effects.

Shared palette, optional font, panel radius and border glow are tunable in `Assets/Game/UI/Themes/MenuTheme.asset`. `GameplayInterface`, `ActiveRunMenu` and `InterfaceFactory` build the additional views once per scene. Existing main-menu layout and preview rendering remain authored. The tutorial floor is `TutorialFloor.mat` using the lightweight `Presentation/Tutorial Floor` shader.

Current regression commands and isolation: [Tests README](../Assets/Game/Tests/README.md).

## Validation entry points

- `Tools/Compile-UnityScripts.ps1`: runtime/editor C# compile.
- Unity EditMode category `RunInterface`: recoverable writes, checksum rejection, discard, state roundtrip, inventory assignment/removal, pause ownership and inventory Back.
- `RunInterfaceSetup.RepairExistingScenes`: explicit authoring command that saves Menu, GamePoc and ConstructionSite; not an ordinary test.
- `RunInterfaceValidation.Run`: isolated Play Mode flow test with screenshots under `Logs/RunInterfaceShots`. Test saves use a separate directory; player saves are not touched.
- `RunInterfaceValidation.BuildAndroid`: development APK under `Builds/RunInterface`.

Device acceptance: test touch drag/drop, joystick/action input, pause/settings/Knowledge ownership, safe quit and Continue, background/foreground, screen lock, extended backgrounding and process termination. Incoming-call and real low-memory OS termination checks require the corresponding device conditions; a force-stop/relaunch test is recorded separately.

## Verified on 18 September 2026

| Check | Result |
| --- | --- |
| Runtime/Editor compile and Android ARM64 development build | Passed; APK installed as an update, without clearing app data |
| New persistence/interface EditMode tests | 12/12 passed, including inactive enemy capture before Awake |
| Complete Editor regression suite | 178/186 passed; eight existing Beam/camera/excavator assertions remain failing |
| Setup/repair idempotence | Passed for Menu, GamePoc and ConstructionSite; stable identities and component counts unchanged on the second pass |
| Play Mode scenario | Passed: fresh entry, pause, inventory, Knowledge, safe quit, Continue, player/ammo/equipment/world restore, lifecycle suspension and confirmed restart |
| Android touch checks (CPH2493, landscape) | Joystick movement, pickup, backpack-to-quick-slot drag, HUD assignment, Inventory Back to Pause, Resume, Settings, Knowledge acquisition/review and acknowledgement passed |
| Safe quit / Continue on Android | Preserved the test run and collected pickup; no arrival sequence replayed; returned paused |
| Extended Android background / return | 187 seconds backgrounded; returned paused with the run intact |
| Screen lock / unlock | Returned paused after the user unlocked the phone |
| Android background, force-stop, relaunch, Continue | Final-build comparison passed for run ID, exact player state, elapsed time, quick slots, world IDs, enemy health/resolution, chest flags and permanent Knowledge |
| Confirmed Hard Restart | New run ID, reset run state, permanent Knowledge retained; Cancel also returned safely |
| New Game warning | Appeared by touch and was cancelled without replacing the run |

Device testing found and fixed an inactive-encounter initialization edge case: capture could read zero health before Unity called an enemy's Awake. Health now initializes once when first captured, and delayed Awake preserves restored health. The final device comparison includes spawned enemies under an inactive encounter container and confirms their health and resolution survive Continue.

Evidence (local, ignored build/test artifacts):

- `Logs/RunInterfaceFinalRegression.xml`: full suite results.
- `Logs/RunInterfaceDeviceFixTests.xml`: 12 focused tests.
- `Logs/RunInterfaceDeviceFixSmoke.log`: final Play Mode scenario passed. This batch editor subsequently crashed during native shutdown after the pass; the fresh Android build process completed successfully.
- `Logs/RunInterfaceDeviceFixBuild.log`: successful final APK build.
- `Logs/DeviceRunInterface/verification.json`: final on-device save comparisons, all true.
- `Logs/DeviceRunInterface/`: screenshots and before/after snapshots; `19-final-inventory.png`, `20-final-restored.png`, `21-final-tutorial.png`, `22-new-game-confirm.png` show the final build.
- `Builds/RunInterface/KidsVsAliens-Development.apk`: installed development build.

No managed gameplay/UI/save exceptions were observed in the final Android flow. Unity's Android startup does log a missing optional Play Asset Delivery `AssetPackManager` class; it did not prevent APK startup or these tests. This pass does not change Android package/plugin configuration.

Remaining release checks:

- Actual incoming call interruption and genuine low-memory OS eviction (force-stop recovery was tested separately).
- iOS lifecycle and a wider set of phone/tablet aspect ratios and performance budgets.
- Full combat/traversal regression with real touch play, including simultaneous movement/aim/fire, grenades and Beam Hoist. Their mechanics and bindings were preserved; this is not a claim of exhaustive device combat validation.
- Future mission/door/set-piece participants when those gameplay systems are authored. There is no invented objective or excavator-mission implementation in this pass.

The eight unrelated full-suite failures are the old vertical-then-curve/beam-source/reach expectations in `BeamTransportTests` / `BeamTransportV2Tests` (five), the camera-output assertion in `InGameMenuTests` (one), and excavator decal renderer-count assertions (two). These were not repaired by changing the protected working gameplay/assets.

The connected phone is left on the restored, paused test run. Its temporary stay-awake-over-USB setting was restored to its original value. The test run and permanently learned Beam Hoist remain available for review.

## Changed files

New Unity assets/components include their corresponding .meta files; existing GUIDs were preserved.

### Persistence foundation

- Assets/Game/Scripts/Persistence/ActiveRunController.cs
- Assets/Game/Scripts/Persistence/PermanentProgress.cs
- Assets/Game/Scripts/Persistence/RecoverableJsonStore.cs
- Assets/Game/Scripts/Persistence/RunContentCatalog.cs
- Assets/Game/Scripts/Persistence/RunSaveData.cs
- Assets/Game/Scripts/Persistence/RunSaveService.cs
- Assets/Game/Scripts/Persistence/RunWorldObject.cs

### UI, presentation and shared theme

- Assets/Game/Materials/TutorialFloor.mat
- Assets/Game/Scripts/Player/InventorySlotUI.cs
- Assets/Game/Scripts/Player/InventoryUI.cs
- Assets/Game/Scripts/Player/PlayerHealthUI.cs
- Assets/Game/Scripts/Presentation/KnowledgePreviewStage.cs
- Assets/Game/Scripts/UI/KnowledgeAcquiredPresenter.cs
- Assets/Game/Scripts/UI/MenuController.cs
- Assets/Game/Shaders/TutorialFloor.shader
- Assets/Game/UI/Components/InterfaceFactory.cs
- Assets/Game/UI/Components/InterfaceGlyph.cs
- Assets/Game/UI/Components/NeonPanel.cs
- Assets/Game/UI/Screens/ActiveRunMenu.cs
- Assets/Game/UI/Screens/GameplayInterface.cs
- Assets/Game/UI/Screens/InGameMenuController.cs
- Assets/Game/UI/Screens/InventoryManagementView.cs
- Assets/Game/UI/Screens/KnowledgeLogView.cs
- Assets/Game/UI/Screens/UIScreenRouter.cs
- Assets/Game/UI/Themes/UITheme.cs

### Gameplay save hooks

- Assets/Game/Scripts/Enemy/AI/EnemyBrain.cs
- Assets/Game/Scripts/Enemy/EnemyHealth.cs
- Assets/Game/Scripts/Enemy/EnemySpawner.cs
- Assets/Game/Scripts/Environment/Chest/LootChest.cs
- Assets/Game/Scripts/Game/GameplaySuspensionController.cs
- Assets/Game/Scripts/Gameplay/PlayerBeamInSequence.cs
- Assets/Game/Scripts/Grenades/GrenadeInstance.cs
- Assets/Game/Scripts/Items/ItemSpawner.cs
- Assets/Game/Scripts/Items/PickupItem.cs
- Assets/Game/Scripts/Player/PlayerGrenadeController.cs
- Assets/Game/Scripts/Player/PlayerHealth.cs
- Assets/Game/Scripts/Player/PlayerInventory.cs
- Assets/Game/Scripts/Player/PlayerMeleeController.cs
- Assets/Game/Scripts/Player/PlayerShooter.cs
- Assets/Game/Scripts/Progression/PlayerSkillState.cs
- Assets/StarterAssets/ThirdPersonController/Scripts/ThirdPersonController.cs

### Scene wiring, catalog, tooling, tests and status

- Assets/Game/Editor/GameplaySceneSetup.cs
- Assets/Game/Editor/Helpers/MenuUISetup.cs
- Assets/Game/Editor/Helpers/RunInterfaceSetup.cs
- Assets/Game/Editor/Helpers/RunInterfaceValidation.cs
- Assets/Game/Resources/RunContentCatalog.asset
- Assets/Game/Scenes/ConstructionSite.unity
- Assets/Game/Scenes/GamePoc.unity
- Assets/Game/Scenes/Menu.unity
- Assets/Game/Tests/Editor/Core/RunPersistenceTests.cs
- PROJECT_CONTEXT.md
- TODO.md

- Docs/RunInterface.md: architecture, tuning, validation evidence and this file inventory.

## Equipment and delayed-hit ownership

`EquippedWeaponChanged` reports actual selection/unequip only. Temporary grenade/melee
appearance uses `WeaponPresentationChanged`; PlayerAnimation listens there. Hiding a gun
does not remove its inventory record or reset reload/cooldown. Grenade selection continues
to block fire through the existing input/action path.

Player shooting resolves physics immediately, then PlayerShooter owns the accepted hit
until its scheduled arrival. The bolt receives the same arrival time calculated from its
configured speed; damage, reaction and impact stay together at arrival. Releasing,
disabling or destroying the visual cannot cancel damage. Missing presentation uses an
explicit fallback travel speed, never instant damage. This remains resolved-ray combat,
not physical projectiles. Its pending-hit coroutine continues while Beam transport disables
the shooting input consumer; a world pause stops both travel and damage through scaled
time. Pending shots are transient across scene teardown/Continue.
