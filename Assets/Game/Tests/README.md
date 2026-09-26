# Regression tests

Run from the repository root in Windows PowerShell, with Unity closed:

```powershell
# Quick: CombatHitResolverTests, VfxPoolTests, PlayerLoadoutStateTests, OwnedWeaponStateTests
.\Tools\Run-UnityTests.ps1 -Suite Quick -ReuseCopy
# Partial Core category (includes scene/render checks; not the full suite)
.\Tools\Run-UnityCoreTests.ps1
# Full regression: every Editor test, with graphics enabled
.\Tools\Run-UnityTests.ps1 -Suite Full -ReuseCopy
# Focused selection (overrides the suite filter)
.\Tools\Run-UnityTests.ps1 -Suite Full -ReuseCopy -TestFilter 'AuditContinueTests;AuditLifecycleTests;RepairPreservationTests'
# Combat Economy, actual Save/Continue/HUD flow and wired audio/import contracts
.\Tools\Run-UnityTests.ps1 -Suite Full -ReuseCopy -TestFilter 'CombatEconomyTests;CombatEconomyPlayTests;CombatEconomyStateTests;CombatEconomyAuthoringTests;AuditContinueTests;UIAudioTests'
```

The runner copies current tracked and untracked, nonignored Assets/Packages/ProjectSettings
into `Logs/RepositoryAuditRemediation/TestProject`. No hard links are used. The first run
without `-ReuseCopy` also copies the import cache, excluding Bee, ShaderCache and PramData.
`-ReuseCopy` refreshes source in the disposable copy and retains its cache; it also works
on first use (imports may take longer). Do not author work in this disposable project.
Move the copy aside to start with a fresh cache. A clean checkout needs the matching
Unity version installed and a valid license; it does not need historical migration logs.
The runner never runs setup/migration entry points before tests.

Results and Unity logs have explicit absolute paths printed at launch, under
`Logs/RepositoryAuditRemediation/<ResultName>-tests.xml` and `-unity.log`.
Use `-ResultName MyRun` for a predictable name; existing XML is never reused.
`KIDS_TEST_SAVE_DIRECTORY` points to a separate `<ResultName>-saves` directory.
The copied project uses a separate company/product identity for Editor PlayerPrefs.
A missing result, failed test, zero tests or nonzero Unity exit is a runner failure.
Graphics are enabled in every suite: **do not add `-nographics`** to render/URP tests.

In an open Editor: Window > General > Test Runner > EditMode > Run All runs full regression.
Core is only one category; RunInterface, LoadoutIntegration, ProceduralUI, AuditRemediation
and uncategorized fixtures also matter. Set `KIDS_TEST_SAVE_DIRECTORY` before launching
an Editor used for tests. Prefer the isolated runner when checking all scene flows.
`Tools/Compile-UnityScripts.ps1` is a separate Roslyn check, not proof of Unity import,
rendering, runtime lifecycle or full test success.

For a manual run that aborts, inspect `Logs/Editor.log` for the first
`REGRESSION FAILURE` (test name, assertion/exception and stack), and the last
`REGRESSION START` without a result. `Internal_CallUpdateFunctions` is only the
Editor dispatcher, not the cause. Batch and manual Editor conditions can differ;
record pause/focus state when a failure only occurs manually. The melee encounter
cases each simulate several minutes; their scaled waits now reject an unexpected
pause rather than waiting indefinitely. Clock-dependent fixtures must establish
their own time scale and restore the previous value, including after failure.

Authoring/import tests use disposable asset copies or preview scenes and clean them up.
The normal weapon geometry/reference test reads current prefabs and data and is
self-contained. The completed historical migration/comparison helper has been retired;
old before/after logs are historical evidence, not a test prerequisite. Never rerun a
mutating migration to create a test oracle.

## Device and manual acceptance

Editor tests do not prove subjective movement, aim, camera, grenade or combat feel,
mobile GPU/memory cost, touch ergonomics, OS termination, native haptics or platform builds.
Check Android touch/pause/Continue/background/lock/termination; preserve visible plasma
travel with simultaneous damage/reaction/impact; inspect fades/silhouettes after reenable.
The Pixel UI/rendering issue remains separate. Profile Realme X2 saves and Beam first
reveal later. Build and verify the renamed iOS native haptics on a Mac.

Tests stay in Editor folders to use the existing predefined Assembly-CSharp assemblies;
no assembly reorganization is required. Add tests for concrete supported contracts and
real regressions, not implementation trivia or fixed content totals.
