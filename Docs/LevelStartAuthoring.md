# LevelStart root authoring

Historical validation results and changed-file lists below describe their original passes. Some one-off helpers have since been retired; see [helper cleanup](LegacyHelperCleanup.md). Current automated validation uses the [Tests README](../Assets/Game/Tests/README.md).

## Contract and workflow

Select **LevelStart** in ConstructionSite. Move its root to the desired final **player-root position** and rotate Y for player facing. Press Play, or start a fresh run. No child movement, coordinate copying, or setup command is needed after moving it.

`PlayerBeamInSequence.ArrivalTransform` always returns its own Transform. There is no serialized destination reference. The existing transport reads this pose synchronously at fresh startup, places Amy at the configured height above it, and finishes at that endpoint. Only yaw is applied to Amy. The existing Beam uses the same endpoint. Control/capsule handoff and arrival timings are unchanged.

Use an upright, unit-scale LevelStart with an unobstructed player capsule and clear vertical arrival path. Fresh arrival accepts an authored endpoint above the floor; it does not require the short ground-support probe used by manual Hoist. Physics remains authoritative for the capsule/path. The root gizmo marks the final player origin and forward direction. After Beam handoff, normal gravity settles an above-ground spawn onto the surface. LevelStart itself is never snapped or rewritten.

## Real saved-scene/menu failure and correction

The initial 12-test pass did not catch the user's failure. Those checks selected poses already passing `IsLandingSafe` and primarily entered Play from ConstructionSite. Moving horizontally over lower terrain while retaining the root's Y exposed the remaining error.

Reproduced without changing gameplay first:

1. Open the actual ConstructionSite asset; move its root to `(-26.85, 0.42240095, -34.9)`, rotate Y to 97 degrees, and **save the scene**.
2. Open Menu before entering Play Mode, avoiding ConstructionSite's unsaved Play Mode backup.
3. Invoke the actual `PlayButton`, `NewGame`, and `Replace` button events. No direct `StartFresh` call or scene repair in this reproduction.
4. Observe scene load, Beam handoff, and nine subsequent runtime samples.

Before correction, New Game correctly loaded `Assets/Game/Scenes/ConstructionSite.unity` in Fresh mode, with no pending restoration and the exact saved LevelStart. The Beam wrapper was there too. But `TryArrival` required `IsLandingSafe(end)`: Hoist's short ground-support probe rejected the root 0.422157 m above the flat terrain. The capsule/descent path was clear. Arrival never began, leaving Amy at the unrelated scene-player pose `(-25.76, 0.65047, -26.13)` with yaw 0. There was no post-arrival teleport: no arrival had started.

Correction: remove that support requirement **only from `TryArrival`**. The existing full capsule sweep still validates the endpoint and descent. Manual Hoist's `IsLandingSafe`, movement, gravity, controller behavior, save/restore, menu callbacks and Beam visuals are untouched. Arrival failure diagnostics now describe collision/path/wiring failures, not missing immediate floor support.

Repeating the exact saved-scene/menu flow produced:

| Observation | Player position | Yaw |
| --- | --- | --- |
| Scene loaded, Beam owns player | (-26.85, 10.42240, -34.9) | 97 degrees |
| Beam handoff | (-26.85, 0.422401, -34.9) | 97 degrees |
| After normal gravity settles | (-26.85, -0.009756, -34.9) | 97 degrees |

Position and yaw error at handoff were zero. No later initialization reset X/Z or heading. The only subsequent position change was normal vertical ground settling; exact standing Y requires authoring the root at the standing player-root height, rather than above the floor. The root remains unchanged throughout. The current four-spiral Beam configuration is preserved.

Evidence: `Logs/LevelStartMenu/baseline-trace.txt` and `fixed-trace.txt`. The reproduction uses copies of existing save data, and restores the original scene bytes afterward. Added **one** regression, `LevelStartMenuFlowTests.SavedRootIsUsedByProductionMenuNewGameAndStaysThereAfterHandoff`: it saves the real scene, starts from Menu, clicks the production replacement flow, requires active arrival despite the short-support-probe miss, verifies exact handoff, then monitors X/Z/yaw for three seconds. It restores the authored scene and save-directory setting across editor domain reloads. Result: **1/1 passed**, including cleanup, with successful Unity process exit (`Logs/LevelStartMenu/regression.xml`). Both baseline and corrected standalone menu reproduction runs also exited successfully.

This follow-up changes only `BeamTransportController.TryArrival`'s support requirement and `PlayerBeamInSequence`'s failure diagnostic, adds `Assets/Game/Tests/Editor/Core/LevelStartMenuFlowTests.cs` with its metadata, and updates these authoring notes. Earlier root-authoring changes listed below remain in place. Temporary test positions and incidental scene serialization changes were removed.

## Root cause and migration

The old adapter and setup treated an independently editable `BeamInSpawn` child as the destination. Previous authoring tests moved that child, not LevelStart. The prefab child had a 0.42240095 m local height offset, so LevelStart itself did not mean “final player root.” Unity's baseline audit found:

| Transform | ConstructionSite world position | Landing/path valid |
| --- | --- | --- |
| Old LevelStart root | (-26.85, 0, -23.39) | No / No |
| Old BeamInSpawn | (-26.85, 0.42240095, -23.39) | Yes / Yes |

The old root could move the child through parenting, but it was not the actual landing pose. A rejected child destination left the player at its original authored position. No fresh-start persistence overwrite or later player-root reset was found in the traced initialization; Continue intentionally bypasses arrival.

The one-time migration removed BeamInSpawn from PF_LevelStart and moved the ConstructionSite root to the existing valid landing position. Its X/Z and facing were retained. The arrival wrapper is now local zero, preserving the existing world placement of the Beam. The gizmo component is on the root. No shared Beam mesh, material, particle, spiral setting, or visual child geometry was changed.

## Entry modes

- **Fresh / New Game:** the root is the endpoint and yaw authority. Existing confirmed replacement discards the old active run before loading.
- **Hard Restart:** the existing fresh-run flow discards the old snapshot and uses the currently authored root.
- **Continue:** the early restoration bypass remains before arrival resolution. Saved player position/facing and world state win; no LevelStart arrival plays. Persistence code/schema is unchanged.

## Setup and ambiguity

The canonical `Tools > Setup > Setup or Repair Active Gameplay Scene` already calls BeamTransportSetup. That helper now repairs root components and VFX/player references, never a separate spawn reference. Existing position/rotation are untouched. A default placement at the existing player pose is used only when LevelStart does not exist.

ConstructionSite has one arrival authority and no BeamInSpawn. GamePoc did not have an arrival owner/controller in the baseline; canonical repair was tested there without saving test changes. It creates one root and preserves later edits on repeat. Duplicate owners or competing named LevelStart objects cause setup to reject the ambiguity. Runtime rejects multiple active owners and diagnoses missing/disabled owners in scenes containing a BeamTransportController. It does not create a runtime spawn marker.

## Unity validation

Unity 6000.5.6f1, isolated save directory; no device testing or production test placements saved.

- 12/12 focused Editor tests: root-authoritative arrival at 0/10/30 m offsets, yaw-only orientation, stale child ignored, missing/duplicate diagnostics, ConstructionSite/GamePoc repair preservation, existing synchronous arrival, and shared Beam particle/spiral cleanup in both directions.
- The Beam regression now reads the authored spiral count (currently 4) rather than assuming the previous default. The asset itself is unchanged.
- Actual Play Mode arrivals at these authored root poses passed endpoint/yaw assertions within 0.001 m / 0.001 degrees at handoff:

| Test | Position | Yaw |
| --- | --- | --- |
| A | (-26.85, 0.42240, -23.39) | 0 degrees |
| B: 10 m move, elevated support | (-26.85, 2.88321, -13.39) | 97 degrees |
| C: 30 m move | (-26.85, 0.00131, 6.61) | 271 degrees |

- Canonical repair twice at every pose preserved transforms and hierarchy counts. GamePoc creation/repeated repair also passed.
- Real Save → Quit/Menu → Continue restored a distinct saved player pose/yaw and enemy health; no arrival/visible Beam.
- Hard Restart and confirmed New Game each discarded the prior snapshot and completed another exact arrival at the currently authored C pose/yaw.
- The Play Mode assertions and result artifact completed successfully. The graphics-enabled Unity batch process returned Windows access-violation exit code `0xC0000005` during shutdown after `Cleanup mono`, as in the preceding Beam review. The focused Editor test process exited successfully. This shutdown issue is not claimed fixed by this authoring change.

Historical evidence: `Logs/LevelStart/audit.log`, `migrate.log`, `tests.xml`, and `play.log`. The one-time migration and scripted review are retired; their authored root placement remains. Use canonical gameplay scene repair and the current LevelStartAuthoringTests/LevelStartMenuFlowTests through the Tests README.

## Changed files

Runtime/authoring assets:

- `Assets/Game/Scripts/Gameplay/PlayerBeamInSequence.cs`
- `Assets/Game/Scripts/Gameplay/BeamTransportController.cs` (arrival yaw only)
- `Assets/Game/Scripts/Gameplay/BeamArrivalPoint.cs`
- `Assets/Game/Editor/Helpers/BeamTransportSetup.cs`
- `Assets/Game/Prefabs/PF_LevelStart.prefab`
- `Assets/Game/Scenes/ConstructionSite.unity`

Tests and explicit review helpers were updated to use the root after removing the old serialized field: `BeamPresentationReview`, `BeamPresentationSetup`, `BeamTransportReview`, `BeamTransportV2PlayReview`, `BeamPresentationTests`, `BeamTransportV2Tests`, and `InGameMenuTests`. Added `LevelStartAuthoringTests` and `LevelStartAuthoringReview` with Unity metadata. This document and the arrival section of `BeamPresentation.md` describe the new contract.

Unrelated gameplay, persistence, UI, camera, enemies, inventory and Beam Hoist behavior are unchanged.
