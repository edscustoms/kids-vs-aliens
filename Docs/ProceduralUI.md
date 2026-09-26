# Procedural UI foundation

Historical validation results and changed-file lists below describe their original passes. Some one-off helpers have since been retired; see [helper cleanup](LegacyHelperCleanup.md). Current automated validation uses the [Tests README](../Assets/Game/Tests/README.md).

Presentation pass, September 2026. The five supplied design sheets guide the visual
language. Only the two icon sheets are imported; panels/buttons contain no raster
shells or baked text. Existing gameplay, saves, pause ownership, inventory operations,
drag/drop, actions and scene flow are unchanged.

## Reuse and tuning

- `NeonPanel` now draws an antialiased signed-distance surface: rounded panel, button,
  circle, joystick rings, slot, gradient fill, divider or badge. The shader adds a
  crisp rim, continuous soft glow, dark fill and optional frame/slot details.
- Normal, selected, empty, disabled and locked are **visual states only**. They do
  not change an item's availability. Pointer feedback observes existing controls.
- `InterfaceGlyph` uses the same shader for heart, armor, crosshair, jump, sprint,
  movement, pause, book, lock and close symbols. Text remains TMP.
- Each graphic is four vertices and shares `Resources/NeonUI.mat`. Style/color data
  is carried in vertex channels, without per-control material instances. Standard
  uGUI stencil and RectMask2D clipping are supported; uGUI may derive stencil materials.
  Meshes update when layout or visual state changes, not continuously for glow.
- Circular graphics use the shorter axis. Original control rectangles, raycast
  targets, joystick handle coordinates and event wiring remain authoritative.
- Decorative replacement shells ignore parent layout groups and never take raycasts.

Tune shared palette, `Surface Accent End`, `Border Thickness`, `Surface Glow`,
`Panel Radius`, and TMP font in `Assets/Game/UI/Themes/MenuTheme.asset`.
Per-surface overrides are on `NeonPanel`. Theme values are copied at construction;
existing prefab overrides are intentionally preserved by setup. Re-enter Play Mode
after changing the theme to rebuild runtime screens. Legacy `Border Glow` remains
serialized for compatibility; the new shader uses `Surface Glow`.

`Assets/Game/UI/Primitives/` contains eleven reusable prefabs:

- NeonPanel, NeonCard, PrimaryButton, SecondaryButton, DangerButton
- CircleControl, JoystickVisual, InventorySlot, ResourceBar, NeonDivider, NeonBadge

Primary and secondary buttons have different border/glow strengths. Button prefabs
have no gameplay actions assigned. JoystickVisual and ResourceBar are presentation
templates, not second input/resource systems. Use the existing controllers.

**Tools > UI > Open Procedural Visual Gallery** opens an unsaved demonstration scene
with surface states, glyphs, all icons and TMP. **Tools > UI > Ensure Procedural Visual
Assets** creates missing assets while preserving existing GUIDs/configuration. The
canonical gameplay repair and menu setup also call this helper.

## Icons

`Assets/Game/UI/Icons/GameplayIcons.png` and `KnowledgeIcons.png` are lossless copies
of the supplied sheets 4 and 5. Unity imports them as ten separately addressable
Sprite regions with source alpha, preserved aspect ratios and mip filtering:

- Items: Fighting, Plasma Pistol, Rifle, Electric Grenade, Medkit.
- Knowledge: Beam Hoist, Fighting, Pistol Handling, Rifle Handling, Electric Grenade.

`Assets/Game/Resources/InterfaceIcons.asset` maps sprites to existing item/skill
references in the UI layer. Unmapped items retain their original icons. Item and
skill ScriptableObjects have not been edited. Medkit is available by its catalog
role; there is no matching current gameplay item to bind automatically.

## Integration and changed files

- `UI/Components/NeonPanel.cs`, `InterfaceGlyph.cs`, `InterfaceFactory.cs`,
  `Buttons/UIButton.cs`; new `NeonVisuals.cs` and `NeonControlFeedback.cs`.
- `Shaders/NeonUI.shader`, `Resources/NeonUI.mat`; `UI/Themes/UITheme.cs` and new
  `InterfaceIconCatalog.cs`, `Resources/InterfaceIcons.asset`.
- `UI/Screens/GameplayInterface.cs`: HUD bars, existing touch controls, Learn badge
  and toast presentation. Shared factories also style Pause, Settings, confirmation
  dialogs, tutorial cards and Active Run without changing their controllers.
- `Scripts/Player/InventoryUI.cs`: quick-bar visual surfaces, states and icon lookup.
- `UI/Screens/InventoryManagementView.cs`: visual slot states and catalog sprites.
  `KnowledgeLogView.cs`: list/detail icons and their presentation space.
- `Editor/GameplaySceneSetup.cs`, `Editor/Helpers/MenuUISetup.cs`: asset setup hook.
  New `Editor/Helpers/ProceduralUISetup.cs`, `ProceduralUIReview.cs` and
  `Tests/Editor/Core/ProceduralUITests.cs`.
- Icon sheets/import metadata, eleven primitive prefabs and corresponding Unity metas.
  Paths above are relative to `Assets/Game/`. No scene, gameplay prefab, item/skill
  data, persistence controller or input controller changes are required.

## Validation and limits

Unity 6000.5.6f1: four focused Editor tests pass (hit rectangle/callback preservation,
shared material/quad, ten transparent icon mappings, stable setup identities).
The visual Play Mode harness uses a disposable save directory and only exercises UI
presentation/navigation. It captures Menu, Active Run presentation, HUD, Pause,
Inventory, Knowledge acquisition/review and the unconfirmed danger modal. Gallery
renders cover 1920x1080, 1280x720 and 2048x1536; HUD and Inventory include smaller
resolution captures. Evidence is under `Logs/ProceduralUI/`, with test results in
`Logs/ProceduralUITests.xml`.

Small Unity review checklist:

- Open the visual gallery; compare 16:9 and 4:3 Game views, circular aspect and edges.
- Hover/press the sample buttons and compare selected/empty/locked slot treatments.
- In ConstructionSite, open Inventory, select an item, then Back and Resume.
- Check the Knowledge icon against the separate inventory Knowledge Book icon.
- Tune the shared theme, re-enter Play Mode and confirm screens use the new values.

The shader reproduces the reference language rather than every painted bevel or
ornament. Icons remain raster artwork by design. Existing screen layouts and TMP
typography remain largely intact. The existing dim tutorial character preview
lighting is unchanged.

No Android/device, lifecycle or deep save/load testing was performed for this pass.
GPU cost on devices is not measured. The final Play Mode walkthrough completed all
checks with an empty runtime error log, then Unity exited with native status
`0xC0000005` during shutdown. Earlier walkthroughs did the same. This is distinct
from the passing UI assertions; the overall Play Mode process exit is not clean.
The focused Editor test process exited normally with all four tests passing.

## Small visual cleanup follow-up

- `NeonVisuals.Icon` gives the existing sprites consistent inset padding and aspect
  preservation. HUD, Inventory and Knowledge use this same presentation helper;
  catalog mappings, sprite regions and source textures are unchanged.
- `InventoryUI` hides the legacy item-name labels only in the compact HUD. Slot
  numbers, quantities and selected/empty visuals remain. Full Inventory keeps names
  and details, with icons separated from the number/name bands.
- `KnowledgeLogView` centers the detail icon and adds breathing room to list rows
  so their frame glow is not cut off at the viewport's initial edges.
- `GameplayInterface` adds the current Knowledge icon beside the existing acquired
  heading, observing `CurrentSkill` without altering tutorial flow or callbacks.
- `TutorialFloor.shader` retains the shared single-quad stage. It now has a dark
  circular pad, antialiased cyan rims, a soft halo, restrained violet accent and
  faint peripheral tech lines. Floor-only edge fading accommodates existing close
  preview framing. No character, animation, camera or lighting settings changed.
  Tune Platform Radius, Tech Line Strength and Rim Glow on `TutorialFloor.mat`.
- The historical scripted walkthrough covered all five Knowledge stages and their
  acknowledge buttons, plus HUD/Inventory/Knowledge captures. That walkthrough is
  retired. `ProceduralUIReview` now provides only **Tools > UI > Open Procedural Visual
  Gallery** and the capture utility used by FightingPlayModeTests.

This cleanup changes the five presentation C# files listed above (including
`InventoryManagementView` for Inventory spacing), the floor shader and the review
harness. Inventory/drag/drop/assignment, Knowledge open/review/close, pause and
touch-control methods were compared against the cleanup baseline and are unchanged.
Validation remains Unity-only.

Final cleanup validation: shader/C# compilation and the Play Mode walkthrough passed
with an empty runtime error log. Visually checked HUD numbers/counts and hidden names,
Inventory two-line names and matching icon scale, Knowledge list/detail padding, and
the pad in all five demonstrations. Inventory/Back/Resume and the five acknowledgement
callbacks completed. See `Logs/UICleanupReview.log` and `Logs/ProceduralUI/` captures.
The final editor process again hit the previously observed native shutdown crash
after successful checks; no gameplay/lifecycle/device investigation was added.

## Inventory and editor-preview cleanup — 19 September 2026

- `KnowledgePreviewStage.cs` and `TutorialFloor.shader`: the shared ground is now
  32 stage units wide (shader `Stage Coverage`), with world-sized pad/grid mapping.
  Removed the small radial ground cutoff. A soft render-edge blend joins the
  existing square character output to its navy frame. Pad radius, camera framing,
  character animations and tutorial behavior are unchanged.
- `PlayerInventory.cs` defaults to 25 backpack positions; five quick assignments
  remain separate. The authored `maxSlots` values in `GamePoc`, `ConstructionSite`
  and `Level_1` are 25. `GameplaySceneSetup.cs` migrates the legacy value of five
  during repair, retaining other intentionally authored capacities.
- `InventoryManagementView.cs` uses the existing `InterfaceFactory.Scroll` with
  `RectMask2D`, `ContentSizeFitter` and the new `BackpackGridLayout.cs`: five columns,
  five logical rows, approximately two visible rows in the current panel. Mouse
  wheel/background dragging scrolls; mouse item dragging retains rearrangement.
  Touch swipes scroll; holding an item for 0.3 seconds begins item dragging.
  Held items can scroll toward off-screen rows at the viewport edges. Full names
  remain in item details; compact cells may ellipsize longer names.
- `PlayerInventory.TryAddItem` rejects already-learned books before insertion.
  `PickupItem.cs` leaves the rejected book in the world and reports the existing
  `KnowledgeAlreadyKnown` feedback. First-time use/unlock/acknowledgement is
  unchanged. Historical saved inventory still restores through `RestoreSavedItems`.
  **No persistence schema or save/load implementation changed**: saves already
  store the item list and five quick-slot indices, without a saved capacity.
- `UIButton.cs` now supports Edit Mode with the same `NeonPanel` renderer used in
  Play Mode. Editor refreshes are coalesced onto `EditorApplication.delayCall`.
  The previous `OnValidate -> ApplyStyle -> NeonVisuals.Replace/Feedback` path
  performed `AddComponent`, parenting and RectTransform writes during Unity
  consistency checks, including Play entry. Those structural operations now run
  after validation. `NeonVisuals.cs` also avoids unnecessary sibling/layout writes.
  `Menu.unity` stores the resulting shells and target graphics; repair reuses them.
  Text-only buttons, functional selector arrows and callbacks are retained.

Validation: seven focused Editor tests passed (`Logs/UICleanupTests.xml`), covering
first-time/duplicate books, 5- and 25-item save representations, quick assignments,
and the existing procedural primitives/icons. The extended `ProceduralUIReview.cs`
and new `UICleanupReview.cs` passed real Unity Play Mode checks for a full backpack,
wheel/touch scrolling, held-touch and mouse item dragging, rearrangement, assignment,
clear, detail selection, Back/Resume, first-time book consumption and rejected world
pickup. All five Knowledge demonstrations and their acknowledgement callbacks ran;
their captures were visually reviewed. Render checks include 720p, 1080p and tablet
sizes. Fixture progression uses a separate test save directory.

The Unity Console was cleared before opening Menu, then checked through object
selection/serialized validation, canvas resizing, repair twice, Play entry and exit.
Hierarchy/component counts stayed stable; Edit/Play procedural style snapshots
matched. Final Console: **0 errors, 1 unrelated warning** (`MobileAimSettings` aim
chances total 85%). No `SendMessage cannot be called` warnings occurred. Both final
Editor-test and Play-review processes exited normally. Evidence:
`Logs/UICleanupPlay.log`, `Logs/ProceduralUI/console.txt`, and the captures in that
folder. No Android/device or lifecycle testing was performed.

Remaining presentation limits: existing dim character lighting and runtime-created
menu character preview are unchanged; this pass provides editor parity for the UI
surfaces, without running gameplay/menu preview spawning in Edit Mode.

Quick manual follow-up: open Menu and compare its buttons across Play entry; scroll
Inventory to row five and drag an item back into a quick slot; acquire/review each
Knowledge skill and confirm the shared pad blends cleanly into the frame.

## Final inventory / Knowledge follow-up

The preceding floor approach still depended on a perspective plane inside a fitted
square RenderTexture, so increasing its size could not fill the wider preview frame.
`TutorialBackdrop.shader` and shared `Resources/TutorialBackdrop.mat` now render a
readable blue/cyan perspective grid across the inner frame. `KnowledgePreviewStage`
places this non-interactive background immediately behind the unchanged character
output. `TutorialFloor.shader` renders only the projected circular pad over that
background. `RunInterfaceSetup` includes idempotent backdrop setup in the canonical
scene repair path. No preview camera, sizing, animation or flow changes are needed.

`InventoryManagementView` now derives a compact display-to-owned-index mapping that
excludes quick-slot assignments. Selection, drag/drop and rearrangement resolve
through that mapping. `PlayerInventory.AssignQuickSlot` moves an existing assignment
instead of swapping the destination's previous item into the source slot; displaced
items become unassigned and reappear in Backpack. Ownership remains one list with
a 25-item limit and five assignment indices. The save schema is unchanged.

Book investigation: the existing `TryAddItem` check already rejects a learned book.
However, `RestoreSavedItems` bypassed that guard and copies obtained before learning
were left behind when one copy was used. Both paths now remove only learned-book
entries, repairing quick indices without rewards or skill events. `PickupItem`
also explicitly calls shared `CanAcceptItem` before attempting insertion or marking
the world pickup removed. The reported fresh-pickup reproduction still needs the
actual prefab/physics check; the restore and pre-learning-copy gaps should not be
misrepresented as a proven explanation of that particular reproduction.

The latest local active-run save inspected during this pass had an empty item list;
permanent data contained all five learned skills. No personal save files were edited.
Normalization runs through the normal inventory restore/use paths.

Validation added: compact middle-item assignment, displaced-item return, assignment
movement without duplicates, clear/restore mappings, pre-learning duplicate copies,
and stale-book restore normalization. The Play Mode harness now exercises the actual
Pistol Book prefab through Unity physics, then reloads fixture permanent data and
checks that a second world book remains uncollected. All fixtures use isolated saves.
Current status: both C# assemblies compile using Unity references. Unity Editor tests,
the updated Play Mode checks and backdrop visual inspection are pending closure of
the editor currently holding this project's lock. Earlier passing results above
describe the preceding pass, not these new changes.

## Floor perspective correction

`TutorialBackdrop.shader` now projects the grid toward a mid-frame horizon: cells
shrink into the distance, with cyan/blue lines fading into a dark upper background.
The existing projected circular pad is unchanged. This correction changes no camera,
layout, animation, tutorial flow or inventory behavior.

Unity-only validation: `KnowledgeFloorReview.Run` entered Play Mode with isolated
fixture saves, captured and visually reviewed Beam Hoist, Fighting, Pistol Handling,
Rifle Handling and Electric Grenade at 1920x1080, plus Rifle Handling at 1280x720.
All five show the floor receding behind the pad across the lower preview, with no
reversed perspective. Both stage shaders compiled, acknowledgement callbacks
completed, and Unity exited successfully. Captures are under
`Logs/ProceduralUI/floor-*.png`; log: `Logs/KnowledgeFloorReview.log`.
This focused review does not validate the unrelated inventory/book follow-up above.

## Screen ownership: where to edit

Paths below are relative to `Assets/Game`. Generated controls are rebuilt when entering
Play Mode: edit their builder, not a transient runtime hierarchy. Shared geometry/color
lives in `UI/Components/InterfaceFactory.cs`, `NeonPanel` and `UI/Themes/MenuTheme.asset`.

| Screen | Human editing entry point | State/action owner |
| --- | --- | --- |
| Main Menu | `Scenes/Menu.unity`, `Scripts/UI/MenuController.cs`, `Editor/Helpers/MenuUISetup.cs`; preview catalogs/settings in `Scripts/UI` | MenuController, PlayerLoadoutState |
| Active Run gate | `UI/Screens/ActiveRunMenu.cs` | RunSaveService; UIScreenRouter |
| HUD | `UI/Screens/GameplayInterface.cs` (`CompactResourceDisplay`) | PlayerHealth |
| Quick Slots | `Scripts/Player/InventoryUI.cs` and `InventorySlotUI.cs` | PlayerInventory assignments; no separate item storage |
| Touch Controls | Authored gameplay canvas and Starter Assets input components; `GameplayInterface.RestyleTouchControls` for the shell | Existing input/action router; repair via GameplayPresentationSetup |
| Pause | `GameplayInterface.BuildPause` creates visible Resume/Settings/Inventory/Quit/Restart | `UI/Screens/InGameMenuController.cs`, suspension lease |
| Gameplay Options | `Editor/Helpers/InGameMenuSetup.cs`, `UI/Screens/OptionsScreenController.cs`, HapticsOptionView/CameraShakeOptionView | Shared camera/haptic settings |
| Menu Options | `Editor/Helpers/MenuUISetup.cs`, OptionsScreenController | Same shared settings; separate screen |
| Inventory | `UI/Screens/InventoryManagementView.cs` | PlayerInventory, InventoryDragSlot |
| Knowledge | `UI/Screens/KnowledgeLogView.cs` | PlayerSkillState, PermanentProgress |
| Tutorial | `Scripts/UI/KnowledgeAcquiredPresenter.cs`, KnowledgePreviewStage, skill tutorial data | Presenter owns modal/demo lifetime; gameplay never depends on preview |
| Feedback | `Scripts/UI/GameplayFeedbackPresenter.cs` and presentation catalog; `GameplayInterface.ShowFeedback` for run toasts | PlayerFeedback/FeedbackScheduler or RunSaveService |
| Restart/reset | `GameplayInterface` builds restart confirmation; `UI/Screens/ProgressResetView.cs` builds reset confirmation | InGameMenuController / ActiveRunController / RunSaveService |

Legacy authored Pause buttons remain in scenes but are hidden by BuildPause. Tests and
future UI work must target the visible generated controls. Do not wire a new feature
by invoking hidden legacy UnityEvents. This table describes existing ownership; no UI
framework or new screen behavior was introduced by the audit remediation.
