# Reusable UI, Options and gameplay cameras

Historical helper/file lists below record earlier implementation passes. Some one-off tools are now retired; see [helper cleanup](../../../Docs/LegacyHelperCleanup.md). Use the [Tests README](../Tests/README.md) for current validation commands.

Implemented 15 September 2026 in Unity 6000.5.6f1 / Cinemachine 2.10.7.

## UI architecture

- `UIButton` composes a standard Unity `Button`. It exposes optional text/icon,
  interactability, selected state and the existing Unity click event. It contains
  no screen, camera, scene-loading or gameplay actions.
- `Btn_Pill` and `Btn_IconCircle` are explicit presentation prefabs. They reuse the
  existing NeonPill/NeonCircle sprites, text treatment and state colors.
- `MenuTheme` provides shared sprite and state-color data. Instance text, image tint,
  fonts/materials and RectTransform dimensions remain authored. Apply theme changes
  on enable, or use `UIButton.ApplyStyle()` when changing a theme at runtime.
- `UISegmentedControl` has arbitrary ID/button options, one selected index and an
  integer selection event. It has no camera dependencies.
- `UIScreenRouter.ShowMainMenu()` / `ShowOptions()` toggle screen roots in Menu.
  There are no navigation animations, fades or camera movements. These entry points
  can later own the planned 3D menu transitions.
- `OptionsScreenController` connects the segment selection to the saved preference.
  The options screen contains only Gameplay Camera and Back.

## Migration and preservation

The eight original buttons (Play, Exit, Select, Preview and four carousel arrows)
are prefab instances using `UIButton`. CHARACTER and AMY nameplates also use the
pill prefab, with passive Button components and their original sibling text objects.
Keeping those text objects preserves their original anchors and controller references.

The existing Unity Button objects and click callbacks remain intact. `MenuController`
was not modified. Menu content is grouped under `MenuCanvas/Screen_MainMenu`, with
the common Background retained outside the screen roots. `Screen_Options` starts hidden.

Before/after native Unity checks passed for all 28 original non-canvas RectTransforms,
13 Images, 10 text labels and eight button state/callback sets. No existing button
was resized or moved. The intentional visual addition is the Options button in the
free lower-right area. The new Options screen uses the common background and styles.
These checks establish preservation of authored properties; no pixel-difference
comparison across platforms was performed.

GameplayPresentationV1 was inspected and retained. No full presentation migration
was performed. The Core presentation/pause/Knowledge regression tests pass.

## Camera configuration

`GameplayCameraController` uses the existing player-following Cinemachine virtual
camera and Transposer. It captures the scene's actual Action settings at Start,
after scene brains register and before their first LateUpdate. It does not replace
the player camera pipeline or change Follow/LookAt references.

| Setting | Action (ConstructionSite baseline) | Tactical initial preset | Isometric initial preset |
| --- | --- | --- | --- |
| Projection | Existing perspective | Perspective | Orthographic |
| Vertical FOV | 40 | 45 | Not used |
| Orthographic half-height | Not used | Not used | 8 |
| Pitch / yaw / roll | 30 / 0 / 0 | 35 / 0 / 0 | 35.26439 / 45 / 0 |
| Follow offset | (0, 3, -7) | (0, 5, -10) | (-8, 8, -8) |
| Position damping | (0.1, 0.1, 0.1) | (0.1, 0.1, 0.1) | (0.1, 0.1, 0.1) |
| Binding | World space | World space | World space |
| Added target offset | None | (0, 0, 0) | (0, 0, 0) |
| Noise | Existing configuration | Existing configuration | Disabled |

Tactical's offset distance increases from about 7.62 to 11.18, with a wider FOV.
Isometric uses the conventional equal-axis orientation and orthographic projection.
Both are first test presets, not final camera tuning. Tune them in
`Assets/Game/Data/Camera/GameplayCameraProfile.asset`. Tune Action on the existing
Cinemachine camera in Edit Mode. Each gameplay scene retains its own Action baseline.

Projection restoration includes the real output camera: Cinemachine's original
inherited projection must return to perspective after an orthographic preset.
Actual Play Mode checks cover this transition.

`GameplayCameraSettings` stores only the mode enum in PlayerPrefs under
`settings.gameplayCameraMode`, saves on selection and defaults invalid/missing data
to Action. No prior settings/persistence service was present. No progression or run
persistence framework was introduced. Application UI has no camera tuning values.

## Setup and naming cleanup

- `Tools/UI/Setup or Repair Active Menu` reuses existing assets/objects and leaves
  existing button geometry intact. Rerunning it does not duplicate controls.
- `Tools/Setup/Setup or Repair Active Gameplay Scene` now calls
  `GameplayCameraSetup`. ConstructionSite and GamePoc are already wired.
- Future gameplay scenes with the standard player-following Transposer camera
  receive the preference controller through the central setup helper. Tuned profiles
  already assigned to a controller are preserved.
- The old acronym prefix was removed from MeshQuickStats, PropAnalyzer,
  CementBagMeshSimplifier, CementBagProjectedPrint.shader, AlienChest_POC_V1.fbx,
  FenceKit_V1.fbx and all associated references. All six metadata GUIDs remain the
  same; both model binaries are byte-identical to their original files.
- Shader paths now include `Environment/Mesh Decal`, `CameraOcclusionLines`,
  `Environment/Cement Bag Projected Print` and `VFX/ElectricAdditive`; lookup strings
  were updated with their shaders.
- Branded Tools menu roots were flattened to `Tools/Helpers`, `Tools/Setup`,
  `Tools/Performance`, `Tools/Environment`, `Tools/Level Tools`,
  `Tools/Construction Site`, and `Tools/Bake Selected Target Colliders`.
- Asset creation menus retain their descriptive groups without a game-name root.
  Editor namespaces use `EditorTools`; AdaptiveIconSetup and the material importer
  identification no longer carry unnecessary branding. Game-title art is unchanged.
- The requested branded ProBuilder helper group does not exist in this checkout's
  Assets or Packages sources, so no such menu entry could be renamed. No placeholder
  tool was created. If that external helper is imported later, use
  `Tools/ProBuilder/Helpers` for its menu group.

No acronym occurrences remain in repository text or filenames. The binary scan also
found incidental matching byte sequences in 185 existing binary files (textures,
models, archives and other binary data). Those bytes are not technical names and
were not rewritten. Ignored generated logs/caches/project files and Git history are
outside the source cleanup; the audit log intentionally records the old names.

## Validation and remaining checks

- Runtime and Editor assemblies compiled successfully.
- All 94 Core EditMode tests passed; zero failed or skipped.
- Native migration validation passed, including repeated Menu/camera setup in
  ConstructionSite and GamePoc.
- Actual Play Mode flow passed: Options, Back, exclusive selection, persistence,
  carousel, Select, Preview, Exit-as-Back, Play into ConstructionSite, applying the
  saved Tactical preference, real orthographic output and restoring Action output.
- Main Menu, Options, Isometric and Action renders were captured and reviewed.
- The user's pre-existing ConstructionSite additions and URP settings were preserved.
  ConstructionSite's only task change is camera-controller wiring. Incidental Unity
  reserialization of legacy GamePoc fields and test time settings was removed.
- Existing warnings include deprecated scene-search APIs, the existing melee test's
  non-serialized HitInfo field, and MobileAimSettings probabilities totaling 85%.
  Unity also logged transient licensing/graphics diagnostics but completed the runs.

No manual Inspector wiring is required for the supplied scenes.

Manual acceptance checklist:

1. Open Menu and compare layout, hover/press/disabled appearance, preview drag,
   both carousels, Select, Preview and Exit (including actual application quit).
2. Open Options, click each mode, return with Back, reopen Options, restart the app
   and confirm the preference persists.
3. Play ConstructionSite in each mode. Tune Tactical/Isometric while checking building
   interiors, stairs, enemies, player readability, camera-relative movement, aiming,
   LOS, shooting and occlusion fading. Repeat with touch controls on Android/iOS.
4. Check Pause/resume, Knowledge tutorial/preview, feedback and Acknowledge in gameplay.
5. On mobile, check narrow/wide landscape ratios, safe areas, touch targets and lifecycle
   interruptions. No device build or device acceptance was performed in this task.

Logs and captures are under `Logs/MenuCameraTask/` (ignored): `migration.log`,
`core-tests.xml`, `tests.log`, `play-review.log`, `naming.json`, `main-menu.png`,
`options.png`, `isometric.png`, `action.png`.

## File manifest

The lists below omit matching `.meta` files. New Unity assets include their metadata.

### New files

- `Assets/Game/Data/Camera/GameplayCameraProfile.asset`
- `Assets/Game/Docs/09-UI-CAMERA-OPTIONS.md`
- `Assets/Game/Editor/Helpers/GameplayCameraSetup.cs`
- `Assets/Game/Editor/Helpers/MenuCameraPlayReview.cs`
- `Assets/Game/Editor/Helpers/MenuCameraValidation.cs`
- `Assets/Game/Editor/Helpers/MenuUISetup.cs`
- `Assets/Game/Scripts/Camera/GameplayCameraController.cs`
- `Assets/Game/Scripts/Camera/GameplayCameraProfile.cs`
- `Assets/Game/Scripts/Camera/GameplayCameraSettings.cs`
- `Assets/Game/Tests/Editor/Core/MenuCameraTests.cs`
- `Assets/Game/UI/Components/Buttons/Btn_IconCircle.prefab`
- `Assets/Game/UI/Components/Buttons/Btn_Pill.prefab`
- `Assets/Game/UI/Components/Buttons/UIButton.cs`
- `Assets/Game/UI/Components/Selection/UISegmentedControl.cs`
- `Assets/Game/UI/Screens/OptionsScreenController.cs`
- `Assets/Game/UI/Screens/UIScreenRouter.cs`
- `Assets/Game/UI/Themes/Btn_IconCircle_Label.mat`
- `Assets/Game/UI/Themes/Btn_Pill_Label.mat`
- `Assets/Game/UI/Themes/MenuTheme.asset`
- `Assets/Game/UI/Themes/UITheme.cs`

### Existing files modified

- `AGENTS.md`
- `Assets/Game/Art/Environment/ConstructionSite/Editor/ConstructionPropLibraryBuilder.cs`
- `Assets/Game/Art/Environment/ConstructionSite/PROP_LIBRARY_SETUP.txt`
- `Assets/Game/Art/Environment/ConstructionSite/PROP_LIBRARY_V5_UPDATE.txt`
- `Assets/Game/Docs/05-FRAMEWORK-REUSE.md`
- `Assets/Game/Docs/07-GAMEPLAY-PRESENTATION.md`
- `Assets/Game/Docs/08-GRENADE-ANIMATION.md`
- `Assets/Game/Docs/EXCAVATOR-DECALS.md`
- `Assets/Game/Editor/BreakableTargetColliderBaker.cs`
- `Assets/Game/Editor/Enemy/EnemyPocSetupWindow.cs`
- `Assets/Game/Editor/ExcavatorDecalReview.cs`
- `Assets/Game/Editor/ExcavatorDecalSetup.cs`
- `Assets/Game/Editor/GameplaySceneSetup.cs`
- `Assets/Game/Editor/GenerateGrenadeThrowAnimation.cs`
- `Assets/Game/Editor/GrenadeThrowMotionReview.cs`
- `Assets/Game/Editor/Helpers/AdaptiveIconSetup.cs`
- `Assets/Game/Editor/Helpers/AlienChestPrefabBuilderWindow.cs`
- `Assets/Game/Editor/Helpers/CharacterSetupHelper.cs`
- `Assets/Game/Editor/Helpers/ElectricGrenadeVfxSetup.cs`
- `Assets/Game/Editor/Helpers/GameplayPresentationSetup.cs`
- `Assets/Game/Editor/Helpers/PolyHavenMaterialImporter.cs`
- `Assets/Game/Editor/Helpers/README_CHEST_PREFAB_BUILDER_V2.md`
- `Assets/Game/Editor/Helpers/UnarmedCombatSetup.cs`
- `Assets/Game/Editor/LevelTools/Fence/ConfigurableFenceSectionEditor.cs`
- `Assets/Game/Editor/LevelTools/Fence/FenceRunEditor.cs`
- `Assets/Game/Editor/LevelTools/Fence/FenceRunSegmentEditor.cs`
- `Assets/Game/Editor/LevelTools/Fence/FenceSectionPrefabBuilderWindow.cs`
- `Assets/Game/Editor/LevelTools/Fence/README_SMART_FENCE_V2_3.md`
- `Assets/Game/Editor/LevelTools/Fence/SmartFenceEditorUtility.cs`
- `Assets/Game/Editor/LevelTools/ModularLevelKit/ModularAlignmentUtility.cs`
- `Assets/Game/Editor/LevelTools/ModularLevelKit/ModularLevelKitGeneratorWindow.cs`
- `Assets/Game/Editor/LevelTools/ModularLevelKit/ModularSnapUtility.cs`
- `Assets/Game/Editor/LevelTools/ModularLevelKit/ModularSnapWindow.cs`
- `Assets/Game/Editor/LevelTools/ModularLevelKit/README_MODULAR_LEVEL_KIT.md`
- `Assets/Game/Prefabs/Environment/Chest/Menu/PF_AlienChest_POC_V1_MenuPreview.prefab`
- `Assets/Game/Prefabs/Environment/Chest/World/PF_AlienChest_POC_V1.prefab`
- `Assets/Game/README.md`
- `Assets/Game/Scenes/ConstructionSite.unity`
- `Assets/Game/Scenes/GamePoc.unity`
- `Assets/Game/Scenes/Menu.unity`
- `Assets/Game/Scripts/Feedback/FeedbackPresentationCatalog.cs`
- `Assets/Game/Scripts/Items/KnowledgeBookItemData.cs`
- `Assets/Game/Scripts/Items/UnarmedCombatItemData.cs`
- `Assets/Game/Scripts/Player/CharacterAnimationActions.cs`
- `Assets/Game/Scripts/PracticeRange/BreakableTarget.cs`
- `Assets/Game/Scripts/Presentation/GrenadePreviewEquipmentAdapter.cs`
- `Assets/Game/Scripts/Presentation/SkillTutorialData.cs`
- `Assets/Game/Scripts/Presentation/WeaponPreviewEquipmentAdapter.cs`
- `Assets/Game/Scripts/Progression/SkillData.cs`
- `Assets/Game/Scripts/UI/MenuPreviewCatalog.cs`
- `Assets/Game/Scripts/UI/MenuPreviewItem.cs`
- `Assets/Game/Scripts/VFX/Grenades/README.txt`
- `Assets/Game/Scripts/World/CameraOcclusionController.cs`
- `Assets/Game/Shaders/ExcavatorMeshDecal.shader`
- `Assets/Game/Shaders/Resources/SH_CameraOcclusionLines.shader`
- `Assets/Game/Shaders/VFX/ElectricAdditive.shader`
- `PROJECT_CONTEXT.md`
- `TODO.md`
- `TODO_SHORT.md`

### Renamed assets

- `Assets/Game/Art/Environment/Chest/AlienChest_POC_V1.fbx`
- `Assets/Game/Art/Environment/ConstructionSite/Shaders/CementBagProjectedPrint.shader`
- `Assets/Game/Art/Environment/Fence/FenceKit_V1.fbx`
- `Assets/Game/Editor/Environment/CementBagMeshSimplifier.cs`
- `Assets/Game/Editor/Performance/MeshQuickStats.cs`
- `Assets/Game/Editor/Performance/PropAnalyzer.cs`
