# Repository audit remediation

This report records the completed remediation baseline. Its historical file list and migration-verification command describe that pass, not today's supported tooling. The later [helper cleanup](LegacyHelperCleanup.md) retires completed migration/review helpers while preserving production regression tests.

Pass 1 discovery completed. Pass 2 adversarial verification completed and is authoritative.
Pass 2 verified **no Critical or High architecture problems**. This pass addresses its
concrete Medium findings and test/documentation/naming debt without redesigning systems.

## Changes by finding

Paths in this table are under `Assets/Game` unless otherwise stated. The complete file
inventory below includes helpers, regression fixtures and Unity metadata.

| Finding | Root problem and files | Final behavior | Regression evidence |
| --- | --- | --- | --- |
| M1 | `Scripts/Player/{PlayerInventory,PlayerEquipment,PlayerShooter}.cs`, new `Scripts/Items/OwnedWeaponState.cs`, `Scripts/Persistence/{ActiveRunController,RunSaveData}.cs`: selecting/spawning a mounted weapon recreated its magazine/reload/cooldown; only selected ammo was saved | Inventory owns one run record per unique weapon definition. Selection retains the record and same-selection retains the mounted instance. All owned magazines/timers save. Legacy selected ammo survives; missing other records use full magazines. Reload continues on scaled time while stowed | OwnedWeaponStateTests; AuditContinueTests actual equipment swap/reselection and Save/Continue |
| M2 | `PlayerShooter.cs`, `Scripts/VFX/PlasmaBoltVFX/PlasmaBoltVFX.cs`: visual lifetime owned accepted hit callbacks | Gameplay owns pending resolved hits; visual receives the same scheduled arrival derived from bolt speed. Damage/reaction/impact remain delayed together. Disable/release/destroy cannot cancel damage; missing visual retains travel delay. A shooter-owned coroutine continues during Beam input suspension and respects world pause | AuditLifecycleTests real raycast -> EnemyHealth/HitReactionProbe, five visual paths, Beam suspension, world pause, zero-length arrival |
| M3 | `PlayerEquipment`, `PlayerAnimation`, `PlayerGrenadeController`, `PlayerMeleeController`: null equipment event also meant hidden presentation | Actual selection/unequip uses EquippedWeaponChanged; animation/style visibility uses WeaponPresentationChanged. Grenade no longer needs a recursion guard for its own presentation notification | AuditContinueTests events/instance/state; GrenadeAnimationTests existing typed marker/cancellation flows |
| M4 | `Scripts/Enemy/AI/EnemyPerception.cs`: reduction depended on unspecified query order | Scan nearest eligible target and nearest valid blocker independently, compare once. Existing masks, samples, low cover, aliens and FOV rules retained | AuditLifecycleTests ordered/reversed real physics hits, blocker behind, low cover and intervening alien |
| M6 | `Scripts/World/CameraOcclusionController.cs`: reenable and duplicate teardown orphaned Active | Most recently enabled controller owns Active; disable removes self, reenable restores, duplicate removal falls back to surviving enabled owner | AuditLifecycleTests actual occlusion/silhouette eligibility before/after lifecycle; silhouette renderer tests |
| M8 | `Editor/Helpers/{GameplayPresentationSetup,FloatingAnimationSetup}.cs`: routine repair replaced authored dependency arrays and retuned loop pose | Merge required references into existing valid order, dedupe, preserve extras. Floating repair leaves importer tuning untouched; no implicit import migration | RepairPreservationTests disposable references/importer/controller and second-run byte equality |
| M9 | `Editor/GameplaySceneSetup.cs`, BeamTransportSetup, CameraFeedbackSetup, ProceduralUISetup, RunInterfaceSetup, CameraOcclusionSilhouetteSetup: fatal checks late, broad saves exceeded scene Undo | Preflight known fatal player/arrival/camera ambiguities. Save specific changed shared assets; valid renderer repair does not flush existing dirty edits. Scene Undo is explicitly not asset/import rollback | RepairPreservationTests fatal central-command preflight and dirty renderer preservation; disposable renderer idempotence tests |
| M10 | `Scripts/Persistence/RunSaveData.cs`, RunInterface and presentation docs: two-pass restore extension rules hidden | Existing restore algorithm retained. Stable keys/IDs, absolute idempotent restore, no reward/resource/completion side effects; OnEnable and first pass may precede peer final state. Peer decisions wait for IsReady | AuditContinueTests saves A->B, actual Continue, both restored twice, final peer values correct, zero reward/event replay |
| M15 | `Scripts/Enemy/EnemyDeathSequence.cs`: renderer.materials privately allocated unowned native materials | Clone explicitly, track only owned copies, destroy them on presentation owner destruction; shared materials stay intact and fade is unchanged | AuditLifecycleTests twelve normal/interrupted death lifetimes, owned copies destroyed without unused-asset cleanup |
| T1 | `Tools/Run-UnityTests.ps1`, Core wrapper, Tests README, RegressionProgress: partial Core looked like full coverage; graphics disabled | Quick has explicit focused classes; Core is partial; Full runs all Editor tests. Graphics enabled; physical isolated project, save directory, results/logs, failure exit handling and per-test progress | Executed runs recorded below; runner syntax and isolation checks |
| T2 | WeaponAlignmentTests and `Editor/WeaponContractRefinement.cs`: normal regression required ignored one-time migration oracle | Current geometry/reference regression reads current data/prefabs from clean checkout. Historical comparison is an explicit separate menu command and needs historical evidence | WeaponAlignmentTests; no geometry-before.json created or required |
| T3 | AudioSystemTests, ProceduralUITests, CameraOcclusionSilhouetteTests, KnowledgePreviewRendererTests, LevelStartMenuFlowTests, DisposableTestAssets: ordinary tests could mutate production imports/renderers/scenes and freeze content totals | Read production contracts, mutate disposable copies. Required/unique event/icon mappings replace fixed totals. Menu flow edits only copied scenes and restores build settings | Focused/full suite; source working-tree hash verification |
| T4 | KnowledgePresentationTests, AlienAuthoringTests, InGameMenuTests; HapticTests frame wait | Already-learned books rejected; no universal scavenging prohibition; exact gate counts/names retained. Visible interactable settings/resume receive pointer clicks. Pump actual player-loop frames; camera output follows authored presets rather than arbitrary positional difference | Focused/full suite; scene acceptance remains separate from reusable contracts |
| Docs/naming | Existing persistence/presentation/UI/occlusion/test/haptic docs; four Beam shaders and their lookup callers; enemy asset menu paths; native haptics binding/file | Small ownership map identifies actual screen editing owners. Truthful cached occlusion and Undo contracts. KVA public shader/menu/native names renamed atomically; GUID-based references retained | Source/reference scan, graphics shader tests; Mac/iOS validation still required |

## What changed architecturally

Ownership moved, not the overall architecture: inventory owns logical weapon state;
equipment selects it; mounted instances present it. PlayerShooter owns accepted delayed
hits independently of cosmetic bolts. Equipment selection and temporary presentation
now have separate direct events. Occlusion has a symmetric deterministic lifecycle owner.
Death presentation owns its private material copies. Repair preserves authored additions
and makes its asset mutation boundary explicit. No event bus, item runtime framework,
projectile framework, transaction framework or persistence dependency graph was added.

## Preserved and deferred

No dead/legacy code removal, broad folder reorganization, Combat Economy, duplicate-weapon
conversion, reload economy, Difficulty, Madness, missions/excavator gameplay, new enemies,
new UI features or balancing. Beam paths/footprints/holes and approved occlusion visuals
are preserved. No spawner change for the disproved runaway-inactive-spawn claim. No
physics-buffer fallback for unsaturated current content. No UI framework replacement.

Deferred, without implementation: device save profiling; Beam first-reveal device
profiling; physics saturation only if future density warrants it; dynamic occluder
membership; released-save migrations; special future Knowledge demos; broad folder
cleanup; dead-code/legacy removal. Current save compatibility is pre-release only.

Manual/device: Android touch/input, pause/Continue, background/lock/termination and
visual timing/fades remain device acceptance. Track the Pixel UI/rendering issue
separately. Realme X2 profiling comes later. iOS needs Mac/Xcode compilation and device
haptics after `KVA_PlayImpact` -> `Kids_PlayImpact` and `KVAHaptics.mm` -> `MobileHaptics.mm`.
No Windows inspection or Editor test is claimed to prove those platform checks.

No requested confirmed code/test fix remains unimplemented. Three old `KVA.*` private
Editor SessionState key strings remain in ConstructionSiteLighting and FightingPlayModeTests;
they are not public type/tool/shader/material/menu/namespace/file names. No prohibited
public naming occurrence remains in the inspected Game/Plugins source or authored assets.
Native binding correctness is still conditional on the Mac/iOS validation above.

## Verification results

Final **Full-03: 318 passed / 318 total, 0 failed, 0 skipped** on Unity 6000.5.6f1,
Windows, Direct3D 12 / NVIDIA GeForce GTX 1070 Ti. This was every Editor test, with
no category/filter exclusion and no `-nographics`. Unity compilation completed with
zero errors. Existing deprecated scene-query warnings remain; no new warning source
was introduced. `git diff --check` passed.

| System (disjoint groups) | Passed / total |
| --- | --- |
| Combat, aliens, melee and LOS | 65 / 65 |
| Audio and haptics | 30 / 30 |
| Persistence and cross-object Continue | 13 / 13 |
| Camera, occlusion and render materials | 23 / 23 |
| Delayed plasma and death-material lifecycle | 2 / 2 |
| Beam, level arrival and authored geometry | 89 / 89 |
| UI, Knowledge, character presentation and feedback | 33 / 33 |
| Grenades | 25 / 25 |
| Input, suspension and VFX pooling | 8 / 8 |
| Weapons, inventory and loadout | 26 / 26 |
| Repair preservation | 4 / 4 |

The plasma/death lifecycle cases contain multiple probes: five VFX paths plus Beam
suspension, world pause and zero-length arrival; twelve normal/interrupted deaths.
The Continue case also covers multi-weapon selection/persistence and equipment events.

Reproduction and outputs:

```powershell
.\Tools\Run-UnityTests.ps1 -Suite Full -ReuseCopy -ResultName Full-03
.\Tools\Run-UnityTests.ps1 -Suite Full -ReuseCopy -ResultName Focused-05 -TestFilter "AuditLifecycleTests;HapticTests"
.\Tools\Run-UnityTests.ps1 -Suite Quick -ReuseCopy -ResultName Quick-01
```

These recorded result names already exist locally; choose new names to rerun. XML/logs
are `Logs/RepositoryAuditRemediation/<name>-tests.xml` and `<name>-unity.log`; each
runner invocation uses its own `<name>-saves` directory. Focused-05 passed **27/27**;
the documented Quick entry point passed **11/11**. Both had zero failed/skipped cases.
PowerShell syntax validation and invalid result-name rejection also passed. Core remains
the explicitly partial category wrapper; it is not represented as a separate full run.
Earlier focused/full failures and their resolutions are retained below.

The canonical repair command also passed an isolated batch smoke check on **GamePoc
and ConstructionSite, twice each**. Component counts stayed at 1,673 and 6,917 between
the first and second calls. SHA-256 snapshots of the disposable project's Assets and
ProjectSettings showed **zero file changes across all four calls**, including no shared
asset/importer/settings writes. Scenes were intentionally not saved. Evidence:
`Logs/RepositoryAuditRemediation/RepairSmoke-unity.log`; the disposable verification
entry point is retained beside it as `AuditRepairSmoke.cs`, outside production Assets.

Final workspace verification compared the original tracked working-tree hashes, not
just HEAD: both pre-existing user edits are identical, no existing production scene,
prefab, ScriptableObject, material, renderer, importer metadata or ProjectSettings bytes
changed, and the renamed native plugin metadata/GUID is identical. The only new Unity
metadata belongs to the listed new code/test fixtures. **Known remaining test failures: 0.**

The first full run completed 317 tests: 307 passed, 10 failed, none skipped.
All ten were pre-existing test/harness assumptions, not production behavior changed by
this pass. The additional dirty-renderer preservation regression brings the final suite
to 318 cases. All passed in the final run above.

| Initial failure | Classification | Correction without production changes |
| --- | --- | --- |
| BeamHoistZoneTests.JumpAndHoist_Unchanged_AndTransportHidesZoneImmediately | stale test missed by cleanup | Measure the selected route after stationary materialization, not the first preview candidate |
| BeamTransportTests.Hoist_IsVerticalThenOneCurve_AndBeamStaysFixed | stale test missed by cleanup | Assert approved single curve and horizontal beam following |
| BeamTransportV2Tests.AbilityLimits_ApplyToDestinationAndFullLift(2,6.1,2,false) | stale test missed by cleanup | Destination height, not decorative control height, owns the limit |
| BeamTransportV2Tests.CanonicalPrefab_FullConeAndNestedLevelStartUseSameSource | stale test missed by cleanup | Validate current authored ProBuilder/source mesh and shared prefab, not an obsolete baked asset path |
| BeamTransportV2Tests.ContextualJump_LockedAndOutOfRangeFallBack_UnlockedHoists_TopReturnsToJump | stale test missed by cleanup | Current materialization/continuous-curve/landing behavior |
| CharacterDemoTests.ActualCharactersSupportSharedContract_AndRealEquipment, Amy and SportyGranny (2) | stale test missed by cleanup | Use the character's style-specific mount, not its fallback socket |
| ExcavatorDecalTests.AuthoredLabelsHaveSeparateOutwardGeometryAndUnmirroredUvs | stale test missed by cleanup | Separate body/label geometry; existing triangle-based surface support replaces a stale body count and absolute X-coordinate assumption |
| FightingPlayModeTests.RealSceneInput_CompleteChains_Movement_KickPlant_AndCancellation | environment/runner failure | Create the test output directory on a clean checkout |
| UIAudioTests.ClickOutcome_IsSingleAndSurvivesSceneUnload | environment/runner failure | Wait for actual player-loop frames, including audio-pool cleanup, instead of Editor coroutine ticks |

Earlier focused-run failures were also fixed:

| Run/result | Failure | Classification |
| --- | --- | --- |
| Focused-02: 79/82 | Enemy haptics fixture checked fire readiness before an actual player-loop frame | environment/runner failure |
| Focused-02 | Menu startup observer was created before entering Play Mode | environment/runner failure |
| Focused-02 | New preflight fixture incorrectly assumed GamePoc already contained a LevelStart | regression caused by this pass (test fixture) |
| Focused-03: 13/15 | New plasma fixture fired before the prior target's deferred destruction | regression caused by this pass (test fixture) |
| Focused-03 | Menu Tactical test required an arbitrary positional difference instead of the authored lens | stale test missed by cleanup |
| Focused-04: 90/91 | Historical decal world-X assertion; actual surface-support check passed | stale test missed by cleanup |
| Focused-01 | Sandboxed Unity launch exited 198 without a test result | environment/runner failure; isolated execution with licensing access succeeded |

No correct production behavior was changed to satisfy these obsolete assertions.

Final source review caught a pass-caused scheduling edge before completion: Beam
transport disables PlayerShooter without pausing world time. The initial LateUpdate
scheduler would therefore delay an accepted hit. A player-owned coroutine fixes that
ownership boundary without adding a manager/component. Focused-05 passed **27/27**
plasma lifecycle and haptic tests, including actual Beam/world-pause suspension leases.
Full-02 was intentionally stopped as superseded, without a completed result; Full-03
tests the corrected final source, also preserving zero-length immediate arrival.

The original `ConstructionSite.unity` and ProBuilder `Settings.json` are preserved by
working-tree SHA-256 baseline. All Unity runs use a physical project copy under ignored
Logs, not the production authoring tree. Historical test logs are not test oracles.

## Complete changed-file inventory

82 file paths changed/added/renamed; the old/new native paths are listed explicitly. No production authoring asset/settings bytes changed.

### Tools and project documentation

- `Assets/Game/Docs/07-GAMEPLAY-PRESENTATION.md`
- `Docs/BeamPresentation.md`
- `Docs/CameraOcclusionSilhouettes.md`
- `Docs/Lit_Fade.md`
- `Docs/MobileHaptics.md`
- `Docs/ProceduralUI.md`
- `Docs/RepositoryAuditRemediation.md`
- `Docs/RunInterface.md`
- `Tools/Run-UnityCoreTests.ps1`
- `Tools/Run-UnityTests.ps1`

### Editor repair and naming

- `Assets/Game/Editor/CameraOcclusionSilhouetteSetup.cs`
- `Assets/Game/Editor/GameplaySceneSetup.cs`
- `Assets/Game/Editor/Helpers/BeamHoistZoneConnectedReview.cs`
- `Assets/Game/Editor/Helpers/BeamHoistZoneSetup.cs`
- `Assets/Game/Editor/Helpers/BeamPresentationReview.cs`
- `Assets/Game/Editor/Helpers/BeamPresentationSetup.cs`
- `Assets/Game/Editor/Helpers/BeamTransportSetup.cs`
- `Assets/Game/Editor/Helpers/CameraFeedbackSetup.cs`
- `Assets/Game/Editor/Helpers/FloatingAnimationSetup.cs`
- `Assets/Game/Editor/Helpers/GameplayPresentationSetup.cs`
- `Assets/Game/Editor/Helpers/ProceduralUISetup.cs`
- `Assets/Game/Editor/Helpers/RunInterfaceSetup.cs`
- `Assets/Game/Editor/WeaponContractRefinement.cs`

### Runtime

- `Assets/Game/Scripts/Enemy/AI/EnemyPerception.cs`
- `Assets/Game/Scripts/Enemy/EnemyAnimationProfile.cs`
- `Assets/Game/Scripts/Enemy/EnemyCombatProfile.cs`
- `Assets/Game/Scripts/Enemy/EnemyDeathSequence.cs`
- `Assets/Game/Scripts/Haptics/MobileHapticBackends.cs`
- `Assets/Game/Scripts/Items/OwnedWeaponState.cs`
- `Assets/Game/Scripts/Items/OwnedWeaponState.cs.meta`
- `Assets/Game/Scripts/Persistence/ActiveRunController.cs`
- `Assets/Game/Scripts/Persistence/RunSaveData.cs`
- `Assets/Game/Scripts/Player/PlayerAnimation.cs`
- `Assets/Game/Scripts/Player/PlayerEquipment.cs`
- `Assets/Game/Scripts/Player/PlayerGrenadeController.cs`
- `Assets/Game/Scripts/Player/PlayerInventory.cs`
- `Assets/Game/Scripts/Player/PlayerMeleeController.cs`
- `Assets/Game/Scripts/Player/PlayerShooter.cs`
- `Assets/Game/Scripts/VFX/PlasmaBoltVFX/PlasmaBoltVFX.cs`
- `Assets/Game/Scripts/World/CameraOcclusionController.cs`

### Shaders and native haptics

- `Assets/Game/Shaders/VFX/BeamEnergy.shader`
- `Assets/Game/Shaders/VFX/BeamHoistZone.shader`
- `Assets/Game/Shaders/VFX/BeamLitFade.shader`
- `Assets/Game/Shaders/VFX/BeamVolume.shader`
- `Assets/Plugins/iOS/KVAHaptics.mm` (renamed to MobileHaptics; original GUID retained)
- `Assets/Plugins/iOS/KVAHaptics.mm.meta` (renamed to MobileHaptics; original GUID retained)
- `Assets/Plugins/iOS/MobileHaptics.mm`
- `Assets/Plugins/iOS/MobileHaptics.mm.meta`

### Tests and test documentation

- `Assets/Game/Tests/Editor/Core/AlienAuthoringTests.cs`
- `Assets/Game/Tests/Editor/Core/AudioSystemTests.cs`
- `Assets/Game/Tests/Editor/Core/AuditContinueTests.cs`
- `Assets/Game/Tests/Editor/Core/AuditContinueTests.cs.meta`
- `Assets/Game/Tests/Editor/Core/AuditLifecycleTests.cs`
- `Assets/Game/Tests/Editor/Core/AuditLifecycleTests.cs.meta`
- `Assets/Game/Tests/Editor/Core/BeamHoistZoneTests.cs`
- `Assets/Game/Tests/Editor/Core/BeamHoistZoneTopologyTests.cs`
- `Assets/Game/Tests/Editor/Core/BeamTransportTests.cs`
- `Assets/Game/Tests/Editor/Core/BeamTransportV2Tests.cs`
- `Assets/Game/Tests/Editor/Core/CameraOcclusionSilhouetteTests.cs`
- `Assets/Game/Tests/Editor/Core/CharacterDemoTests.cs`
- `Assets/Game/Tests/Editor/Core/DisposableTestAssets.cs`
- `Assets/Game/Tests/Editor/Core/DisposableTestAssets.cs.meta`
- `Assets/Game/Tests/Editor/Core/ExcavatorDecalTests.cs`
- `Assets/Game/Tests/Editor/Core/FightingPlayModeTests.cs`
- `Assets/Game/Tests/Editor/Core/GrenadeAnimationTests.cs`
- `Assets/Game/Tests/Editor/Core/HapticTests.cs`
- `Assets/Game/Tests/Editor/Core/InGameMenuTests.cs`
- `Assets/Game/Tests/Editor/Core/KnowledgePresentationTests.cs`
- `Assets/Game/Tests/Editor/Core/KnowledgePreviewRendererTests.cs`
- `Assets/Game/Tests/Editor/Core/LevelStartMenuFlowTests.cs`
- `Assets/Game/Tests/Editor/Core/OwnedWeaponStateTests.cs`
- `Assets/Game/Tests/Editor/Core/OwnedWeaponStateTests.cs.meta`
- `Assets/Game/Tests/Editor/Core/ProceduralUITests.cs`
- `Assets/Game/Tests/Editor/Core/RegressionProgress.cs`
- `Assets/Game/Tests/Editor/Core/RegressionProgress.cs.meta`
- `Assets/Game/Tests/Editor/Core/RepairPreservationTests.cs`
- `Assets/Game/Tests/Editor/Core/RepairPreservationTests.cs.meta`
- `Assets/Game/Tests/Editor/Core/UIAudioTests.cs`
- `Assets/Game/Tests/Editor/Core/WeaponAlignmentTests.cs`
- `Assets/Game/Tests/README.md`
- `Assets/Game/Tests/RestorePeerProbe.cs`
- `Assets/Game/Tests/RestorePeerProbe.cs.meta`
