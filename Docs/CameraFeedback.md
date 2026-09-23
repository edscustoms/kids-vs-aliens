# Camera Feedback / Screen Shake V1

Implemented 24 September 2026. Local-player presentation only.

## Integration

The existing Cinemachine 2.10.7 Brain, virtual camera, camera modes and authored follow motion remain authoritative. PlayerAim reads the output camera for mouse rays, mobile visibility and movement-facing calculations; ThirdPersonController also reads its forward/right vectors. Applying ordinary persistent Cinemachine noise would therefore feed feedback into gameplay.

Instead, CameraFeedbackController on the existing MainCamera applies a temporary pose in URP's `beginCameraRendering` callback, after gameplay and Cinemachine updates, and restores the exact local pose in `endCameraRendering`. The installed URP 17.5 implementation initializes its camera data inside this callback scope. The render gets the offset; gameplay, camera mode state, occlusion queries and saves get the underlying pose. No alternate camera, render feature, camera hierarchy rewrite or changed aiming math is needed. See [Unity's rendering callbacks](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/rendering/renderpipelinemanager/endcamerarendering).

Each request snapshots a deterministic impulse, with immediate kick and smooth finite recovery. There is no random shake and no gameplay RNG use. A fixed 16-entry array bounds overlapping events; aggregate rotation and translation have configurable limits. Offsets are recomputed from the latest underlying pose each render, never accumulated into a transform.

Pause/suspension (including Beam), background/focus loss, camera mode changes, disable and scene teardown clear all impulses and restore any applied pose. End-of-render-context and an early Update provide additional restoration guards. Effects are transient and are never saved or replayed by Continue.

## Trigger points

- `PlayerShooter.Shoot()`: one `equippedWeapon.fireCameraFeedback` request next to muzzle VFX/audio/haptics after accepted aim and muzzle-safety resolution. Every automatic bullet runs this same path. Rejections and muzzle obstruction request none. Existing obstructed-shot ammo/impact behavior remains unchanged. EnemyRangedAttack and preview firing do not request recoil.
- `PlayerHealth.ApplyDamage()`: one PlayerDamage request when health or armor actually decreases. Both ReceiveDamage and the unchanged public TakeDamage entry point route here. ReceiveDamage passes the existing HitInfo.Direction; direct damage uses a neutral direction. A small configured yaw/roll contribution can reflect the incoming direction. Restore, zero/negative/non-finite damage and damage after death do not request feedback. Non-finite damage is now rejected before mutation to prevent invalid input from corrupting health; valid damage math is unchanged.
- `ThirdPersonController.Move()`: the new Landed event reports pre-collision downward velocity only when CharacterController.Move finds new walkable ground. It observes the existing collision flags/contact normals; it does not alter gravity, controller movement, slope classification or jumping. CameraFeedbackController listens only to its configured local player, applies suspension gating, then dispatches speed-scaled HardLanding feedback through the service.

## Tuning

Profiles and shared limits are under `Assets/Game/Resources/CameraFeedback/`. WeaponItemData owns the PistolFire/RifleFire references; Config owns PlayerDamage/HardLanding and landing thresholds.

| Profile | Duration | Local rotation (degrees) | Local position (metres) |
| --- | --- | --- | --- |
| PistolFire | 0.14 s | X -0.32 | None |
| RifleFire | 0.10 s | X -0.12 | None |
| PlayerDamage | 0.23 s | X 0.75, Z 0.20; directional influence 0.25 | Y -0.015 |
| HardLanding | 0.22 s | X 0.50 | Y -0.035 |

Negative camera-local X pitches the view upward. Landing strength is zero at/below 8 m/s downward, scales to full at 16 m/s, and remains capped above that. Combined feedback is limited to 1.5 degrees and 0.06 metres. These conservative starting values require physical-phone tuning.

## CAMERA SHAKE setting / setup

Default ON. `CameraFeedbackSettings.Enabled` writes PlayerPrefs `settings.cameraShakeEnabled` and saves immediately, matching the existing camera/haptics preferences. OFF suppresses every V1 request and clears active offsets immediately. It is independent of Haptics Enabled, camera mode, progression and run saves. Changing the setting produces no feedback.

The option is available in Main Menu Options and In-Game Options. Existing screen controllers ensure it at runtime; MenuUISetup and InGameMenuSetup ensure it during repair. The original generated in-game haptics row is split to fit the two controls. Custom haptics transforms are preserved. Confirmation overlays remain above the setting.

Canonical `Tools > Setup > Setup or Repair Active Gameplay Scene` calls CameraFeedbackSetup in the same task. The helper reuses one controller on the existing MainCamera/Brain and repairs local movement and suspension references. GamePoc and ConstructionSite are already wired. Other gameplay scenes receive the component by rerunning canonical setup. No profile tuning is rewritten by repair.

## Files

Added with metadata:

- `Assets/Game/Scripts/Camera/CameraFeedback{Profile,Config,Settings,Service,Controller}.cs`
- `Assets/Game/Resources/CameraFeedback/{Config,PistolFire,RifleFire,PlayerDamage,HardLanding}.asset`
- `Assets/Game/UI/Screens/CameraShakeOptionView.cs`
- `Assets/Game/Editor/Helpers/CameraFeedbackSetup.cs`
- `Assets/Game/Tests/Editor/Core/CameraFeedbackTests.cs`
- This document.

Changed:

- `Assets/Game/Scripts/Items/WeaponItemData.cs`
- `Assets/Game/Items/Weapons/{PlasmaPistolItem,PlasmaRifleItem}.asset`
- `Assets/Game/Scripts/Player/{PlayerShooter,PlayerHealth}.cs`
- `Assets/StarterAssets/ThirdPersonController/Scripts/ThirdPersonController.cs` (observation event only)
- `Assets/Game/UI/Screens/{OptionsScreenController,InGameMenuController}.cs`
- `Assets/Game/Editor/Helpers/{MenuUISetup,InGameMenuSetup}.cs`
- `Assets/Game/Editor/GameplaySceneSetup.cs`
- `Assets/Game/Scenes/{GamePoc,ConstructionSite}.unity`
- `Assets/Game/Tests/Editor/Core/HapticTests.cs` (shared real shooting/damage/enemy fixtures now also assert camera requests).

## Validation and phone checklist

Runtime/Editor C# compilation passed. The final Unity run passed **32/32** focused tests (`Logs/CameraFeedback-final-focused-tests.xml`): 14 camera tests and 18 shared shooting/damage/haptic tests. Coverage includes accepted and rejected pistol fire, per-bullet automatic fire, actual enemy bullets, real damage and existing hit direction, invalid/restore/dead-player damage, settings persistence/suppression, both Options builders, both scene repairs, bounded overlap, exact pose restoration, interruption cleanup, physical small/hard falls and an actual URP render callback observing the kick followed by a restored aim ray. The physical fall fixture uses a deterministic 60 Hz simulation step; this is not a device performance claim.

Existing combat/menu/camera/input regressions passed **12/13** in `Logs/CameraFeedback-tests.xml`. The sole remaining failure is the already documented `InGameMenuTests.ActualScenes_CameraOutputChangesWhilePaused_AndOnlyResumeUnblocksInput` at line 198: Tactical displacement about 0.03 rather than greater than 1. This previously reproduced with the original menu controller during Haptics V1 (`Logs/Haptics-camera-baseline-tests.xml`). No unrelated camera fix was attempted.

Both shipped gameplay scenes were repaired through CameraFeedbackSetup. Their serialized changes contain only the new controller and its player/suspension references. `git diff --check` passed. No physical phone validation has been performed.

On OnePlus Nord 3, check:

1. Pistol gives a tiny sharp upward kick; each rifle bullet is weaker and sustained fire remains readable. Verify empty/cooldown/rejected/muzzle-blocked firing stays still.
2. Actual armor/health loss gives a stronger short impact. Enemy firing alone and Knowledge previews remain still.
3. Walking, steps and ordinary small jumps stay still. Test progressively higher physical drops, including sloped landing geometry.
4. Turn CAMERA SHAKE OFF in each Options screen; firing, damage and falls continue normally with no feedback. Relaunch/Continue and verify the preference persists. Haptics remain independently selectable.
5. During recoil/impact, pause, background, switch camera mode, exit/reload and perform Beam transport; verify no residual offset or resumed old effect. Check Action/Tactical/Isometric and touch aim readability.
6. Review both Options layouts at device aspect ratio/safe area, and confirm progress-reset confirmation covers the new controls. Check device frame pacing during sustained rifle fire.

Final perceived feel and device behavior are not claimed from automated tests.
