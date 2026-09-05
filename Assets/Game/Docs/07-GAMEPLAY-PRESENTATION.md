# Knowledge, Feedback and Pause V1

Implementation status: 5 September 2026. Phases 1–7 are complete in code, assets,
Editor setup and documentation. Final runtime/Editor compilation passed and all
43 Core EditMode tests passed (0 failures, 0 skipped), including integrated
Knowledge Book/manual-pause behavior. Results: `Logs/Presentation-Final-tests.xml`.
Visual appearance, actual rendering and device input still require the checks below.

## Setup in the existing Unity project

1. Open a gameplay scene containing exactly one `PlayerCharacter`, outside Play Mode.
2. Run **Tools → Kids VS Aliens → Setup → Knowledge Feedback & Pause V1**.
3. Inspect the selected `GameplayPresentationV1` root and save the scene yourself.
4. Enter Play Mode. Escape toggles desktop pause; mobile uses the top-right Pause/Play button.

There is no additional Inspector wiring for the existing player configuration.
Run setup once in each gameplay scene that should use this presentation. Menu scenes
do not need it. The command marks the scene dirty and supports scene Undo; it does
not save gameplay scenes or edit gameplay prefabs. Project data assets are saved.

Reruns repair references and reuse named objects, components, EventSystems and
data assets. **Reruns reapply generated UI layout, typography, colors and preview
camera/light defaults.** Stop rerunning after manual UI polish, or checkpoint the
scene first and review/Undo those changes. Existing tutorial/catalog asset values
are preserved. This is intentionally a small V1 setup command rather than a UI
authoring migration system.

## Runtime responsibilities

- `PlayerFeedback` carries semantic events; `FeedbackPresentationCatalog` owns
  wording/policy and `FeedbackScheduler` owns dedupe, cooldown, priority and an
  optional bounded queue. The presenter formats/fades using unscaled time.
- `GameplaySuspensionController` owns time, cursor, input blocking and independent
  disposable leases. Only the last release restores the previous state. Manual
  pause and Knowledge can coexist. Desktop pause cannot toggle through a modal.
- `StarterAssetsInputs` continues ingesting raw input while blocking gameplay.
  Suspension cancels FIRE rather than emitting the normal release that throws a
  grenade. Resume suppresses the dismissal frame and requires held input to return
  to neutral. `PlayerPrimaryActionRouter` and `PlayerGrenadeController` remain enabled:
  suspension cancels charge back to Held, preserving selection and its visual.
  Ordinary `CancelThrow` still exits grenade mode.
- `KnowledgeAcquiredPresenter` queues successful unlock events and opens only after
  the acquisition frame. Inventory/progression completes first. Missing tutorial or
  incompatible preview content falls back to acknowledgement without undoing skill
  acquisition. Closing releases only Knowledge's lease.
- `SkillDemoPlayer` uses the current character's source visual prefab, actual
  equipment, avatar/controller and shared `CharacterAnimatorDriver`. No live player
  clone, inventory/ammo usage, projectiles, hit resolution or progression is run.
- `PreviewStageContent` shares model-only framing and attachment infrastructure
  with the existing `MenuPreviewStage` façade; menu serialized references remain
  intact. Dynamic line/particle/trail bounds do not affect framing.

## Preview rendering and content

One transient 512 × 512 RenderTexture, one actor and one camera exist while shown.
Closing disables the camera/light/root, clears the actor and releases the texture.
The preview Animator uses unscaled time, no root motion and no animation events.
Optional arcs, particles, audio, colliders and gameplay-like scripts are excluded
or disabled on the isolated preview instance; shared gameplay VFX code is unchanged.

The existing `Assets/Game/Settings/Rendering/Mobile_Renderer.asset` has no renderer
features. Setup registers it in each configured URP pipeline if absent. In the
current assets it is desktop index 1 and mobile index 0. The desktop gameplay
renderer and its SSAO remain at default index 0. The camera selects the matching
renderer per active pipeline and refuses to render if the reference is unavailable
or acquires renderer features. Post-processing/shadows/depth/color requests are off.

`KnowledgePreview` is Unity layer 9 in the current project. Scene cameras/lights
exclude it. Preview renderers and the local point light use rendering mask 128
(existing Light Layer 7); current gameplay renderers/lights use Default. Keep that
rendering layer reserved. The point light cannot replace URP's directional main light.
Actual GPU isolation/appearance needs the Frame Debugger check below.

Initial content is **existing stance only**: Amy/Granny with the real pistol,
rifle, or held electric grenade. Fire/throw/punch/kick clips do not exist in the
current shared animation contract; no fabricated animations or new controllers
were introduced. Actor scale, lighting and UI spacing are V1 defaults to playtest.

## Author another skill

Create `SkillData` and `SkillTutorialData`, link the optional tutorial reference,
choose equipment/action and enter text/framing. Compatible characters use their
own source visual prefab automatically. Unknown MonoBehaviours fail the preview
safety check before instantiation; review the safety policy when extending visual
prefab capabilities. A new equipment family gets one `PreviewEquipmentAdapter`.
Future real actions use `CharacterAnimationActions` on `CharacterVisual` to map a
stable `CharacterActionId` to a valid trigger in the shared gameplay controller.
No change to `KnowledgeAcquiredPresenter` is needed for ordinary content additions.

## Changed files

New runtime files (each has its Unity `.meta`):

- `Scripts/Feedback/`: `GameplayFeedbackEvent`, `PlayerFeedback`,
  `FeedbackPresentationCatalog`, `FeedbackScheduler`.
- `Scripts/Game/GameplaySuspensionController.cs`.
- `Scripts/Player/`: `CharacterActionId`, `CharacterAnimationActions`, `CharacterAnimatorDriver`.
- `Scripts/Presentation/`: `SkillTutorialData`, `KnowledgePresentationQueue`,
  `PreviewStageContent`, `KnowledgePreviewStage`, `PreviewVisualSafety`,
  `PreviewEquipmentAdapter`, `WeaponPreviewEquipmentAdapter`,
  `GrenadePreviewEquipmentAdapter`, `SkillDemoPlayer`.
- `Scripts/UI/`: `GameplayFeedbackPresenter`, `GameplayPointerInputFilter`,
  `GameplayPresentationLifetime`, `KnowledgeAcquiredPresenter`, `ManualPauseButton`,
  `PlayIconGraphic`, `SafeAreaPanel`, `SuspensionHudBinding`.

Modified runtime files:

- `Scripts/Items/PickupItem.cs`, `WeaponInstance.cs`.
- `Scripts/Player/CharacterVisual.cs`, `PlayerAnimation.cs`, `PlayerCharacter.cs`,
  `PlayerGrenadeController.cs`, `PlayerInventory.cs`, `PlayerPrimaryActionRouter.cs`,
  `PlayerShooter.cs`.
- `Scripts/Enemy/AI/EnemyBrain.cs`, `Scripts/Enemy/EnemyMovement.cs`.
- `Scripts/Progression/SkillData.cs`, `Scripts/UI/MenuPreviewStage.cs`.
- `Assets/StarterAssets/InputSystem/StarterAssetsInputs.cs` and `StarterAssets.inputactions`.

Editor/tests/tools:

- `Editor/Helpers/GameplayPresentationSetup.cs`.
- `Tests/Editor/Core/`: `FeedbackSchedulerTests`, `GameplaySuspensionTests`,
  `GrenadeSuspensionTests`, `KnowledgePresentationTests`, `PreviewStageTests`,
  `CharacterDemoTests`, `GameplayPresentationSetupTests`.
- Repository `Tools/Compile-UnityScripts.ps1` and `Tools/Run-UnityCoreTests.ps1`.
- This document, `Tests/README.md`, repository `PROJECT_CONTEXT.md` and `TODO.md`.

Assets/settings:

- Six small assets under `Data/Presentation/`: feedback catalog, weapon/grenade
  adapters and Pistol/Rifle/Grenade Handling tutorials.
- Three existing `Data/Progression/*Handling.asset` tutorial references.
- `Settings/Rendering/PC_RPAsset.asset`: adds the existing mobile renderer reference.
- `ProjectSettings/TagManager.asset`: adds `KnowledgePreview` in an unused layer slot.
- No gameplay `.unity`, prefab, animation controller or large art assets changed.

Paths above are relative to `Assets/Game/` unless stated otherwise.

## Verification and remaining acceptance checks

Compile helper uses installed Roslyn and the existing generated project/Unity
assembly references; it does not create a Unity project or copy Assets. Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Compile-UnityScripts.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-UnityCoreTests.ps1 -ResultName Presentation-Final
```

Close Unity before batch tests, or use the already-open Editor's Test Runner,
EditMode, Core category. The batch helper uses this project and preserves tracked
performance-test resources/time settings that Unity's test execution rewrites.
Logs/results are under ignored `Logs/`; compile output is under ignored `Temp/`.
No Library rebuild or separate project/import is used.

Automated coverage includes feedback scheduling, nested suspension and cleanup,
fresh input after resume, actual Held/Charging grenade selection preservation with
no consumption/spawn, legacy immediate melee pause safety, queued acquisition,
model bounds/menu loadout behavior, Amy/Granny controller/attachment compatibility,
setup twice/no duplicates, renderer selection in both pipelines, camera hidden/no
texture allocation, separate safe-area containers, no scene save, and Escape binding.
EditMode lifecycle tests invoke ordinary MonoBehaviour callbacks explicitly; they
do not prove Unity's runtime callback ordering, rendering or touch dispatch.

Before accepting V1, test:

1. **Desktop Play Mode:** Escape pause/resume with the cursor locked, then the HUD
   button with it unlocked. Repeat while moving/firing and with grenade Held or
   Charging. No throw, consumption, deselection or stale FIRE on pause/resume.
2. **Knowledge + pause:** learn each book with Amy and Granny, close with GOT IT,
   confirm the book is consumed once and Knowledge persists. Test a queued unlock
   followed by manual pause: closing Knowledge must leave manual pause active.
   Escape must not toggle manual ownership through the modal. Test missing data,
   duplicate books, sequential unlocks and scene/player teardown while paused.
3. **Combat/input regression:** pistol/rifle equip/fire, ammo/reload preservation,
   aim/target switching, mobile primary routing, grenade select/charge/full cancel/
   release, inert result/recovery/pickup, carry visuals and inventory slots. Check
   one missing-skill feedback per FIRE hold and no inventory-full spam.
4. **Preview/menu:** correct source character, grip and stance; inspect menu
   character/weapon/grenade previews for framing regressions. In Frame Debugger,
   confirm the Knowledge camera has no SSAO pass, no gameplay light leakage and
   no rendering after close; check repeated opens for actor/texture leaks.
5. **Android/iOS device:** pause and FIRE multi-touch/release order, safe areas,
   landscape aspect ratios/notches, text fitting, preview cost, focus/background
   recovery, repeated pause/overlay transitions and scene changes.

## Narrow regression findings

Legacy `Enemy.prefab` still contains `EnemyMovement` and is referenced by spawners
in GamePoc, Level_1, Level_2_POC and Level_3_POC. Its Update now returns while time
is paused, matching the already guarded `EnemyBrain` immediate-melee path.
PracticeTarget fires through scaled `WaitForSeconds`; PlasmaBolt arrival depends
on scaled travel; timed grenade activation depends on scaled fuse time and impact
activation on physics. The narrow damage-path review found no additional active
Update-driven direct health mutation requiring an unrelated redesign.

Existing Knowledge persistence remains app-session POC state, not a new disk-save
system. Existing proficiency/reset design, shared PlasmaCore effects, rifle support
hand IK and missing final action animations were not changed by this feature.
