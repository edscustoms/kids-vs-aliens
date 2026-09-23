# Mobile Haptics V1

Implemented 23 September 2026. The service has no scene object, Update loop, or input callbacks.

## Wiring and tuning

`WeaponItemData.fireHaptic` references the weapon's profile. `PlayerShooter.Shoot()` requests it beside the existing muzzle VFX/fire audio, after aim acceptance and the physical muzzle-safety query. Automatic fire requests one pulse each time it emits a bullet; there is no burst-wide vibration or haptic cooldown. The existing enemy and Knowledge-preview paths never request fire haptics.

Muzzle obstruction previously consumed ammunition and resolved a wall impact. That behavior remains intact, but the obstructed shot requests no haptic. Ordinary emitted bullets can still hit world geometry downrange; hits remain physics-authoritative.

`PlayerHealth.TakeDamage()` requests the damage profile only when current armor or health actually decreases. Both direct callers and `IDamageable.ReceiveDamage()` use this path. Restore, zero/negative damage, and repeated damage after death do not pulse. An enemy bullet that damages the player can therefore cause **damage** feedback, never enemy **fire** feedback.

All initial feel is editable in `Assets/Game/Resources/Haptics/`:

| Profile | Android duration | Android amplitude | iOS style | iOS intensity |
| --- | ---: | ---: | --- | ---: |
| PistolFire | 18 ms | 100 / 255 | Light | 0.70 |
| RifleFire | 8 ms | 50 / 255 | Light | 0.35 |
| PlayerDamage | 45 ms | 210 / 255 | Heavy | 1.00 |

These are starting values, not device-verified equivalents between platforms. iOS impact generators choose their own pulse duration. The rifle uses lower intensity with the light style; Android also uses a shorter duration.

## Platforms

Android uses cached JNI access to `VibratorManager.getDefaultVibrator()` on API 31+, or the vibrator system service on older devices. API 26+ uses `VibrationEffect.createOneShot(duration, amplitude)`, checking amplitude support first. Devices without amplitude control use the default amplitude with the configured short duration. Older APIs, or a failed effect API, use `Vibrator.vibrate(long)`. Missing hardware/API access silently no-ops. No `Handheld.Vibrate` is used. The build callback adds `android.permission.VIBRATE` once to the generated Unity library manifest without replacing its activity configuration. See [Android vibration APIs](https://developer.android.com/reference/android/os/Vibrator) and [VibrationEffect](https://developer.android.com/reference/android/os/VibrationEffect).

iOS uses an iOS-only Objective-C++ plugin with cached light/medium/heavy `UIImpactFeedbackGenerator` instances, prepares them for subsequent impacts, and uses intensity on iOS 13+. Older supported systems use the selected impact style. Inactive applications do not pulse. UIKit handles unsupported hardware; the C# bridge also no-ops if the native entry point is unavailable. See [Apple impact feedback](https://developer.apple.com/documentation/uikit/uiimpactfeedbackgenerator).

Editor and other build targets select a silent backend. Editor mobile input emulation cannot activate native vibration.

## Setting and scene setup

`HapticSettings.Enabled` defaults ON and saves immediately to PlayerPrefs key `settings.hapticsEnabled`, matching the existing camera preference approach. This preference is independent from run/progression saves and resets. OFF suppresses requests before backend dispatch.

Both main-menu Options and in-game Options contain a silent ON/OFF button. Their existing controllers create it for older authored scenes; the existing MenuUISetup/InGameMenuSetup repair paths also add it idempotently. Canonical GameplaySceneSetup already invokes InGameMenuSetup through GameplayPresentationSetup. No manual component wiring, scene serialization migration, generic HUD feedback, or strength slider is required.

## File inventory

Added (with Unity metadata):

- `Assets/Game/Scripts/Haptics/HapticProfile.cs`
- `Assets/Game/Scripts/Haptics/HapticSettings.cs`
- `Assets/Game/Scripts/Haptics/HapticService.cs`
- `Assets/Game/Scripts/Haptics/MobileHapticBackends.cs`
- `Assets/Game/Resources/Haptics/{PistolFire,RifleFire,PlayerDamage}.asset`
- `Assets/Plugins/iOS/KVAHaptics.mm`
- `Assets/Game/Editor/HapticAndroidBuild.cs`
- `Assets/Game/UI/Screens/HapticsOptionView.cs`
- `Assets/Game/Tests/Editor/Core/HapticTests.cs`
- This document.

Changed:

- `Assets/Game/Scripts/Items/WeaponItemData.cs`
- `Assets/Game/Items/Weapons/{PlasmaPistolItem,PlasmaRifleItem}.asset`
- `Assets/Game/Scripts/Player/{PlayerShooter,PlayerHealth}.cs`
- `Assets/Game/UI/Screens/{OptionsScreenController,InGameMenuController}.cs`
- `Assets/Game/Editor/Helpers/{MenuUISetup,InGameMenuSetup}.cs`
- `Assets/Game/Editor/GameplaySceneSetup.cs` (documents the existing delegated repair path).

## Validation

- Runtime and Editor C# compilation passed; existing unrelated warnings remain.
- Android, iOS, and standalone C# haptic branches separately compiled against installed Unity assemblies.
- Unity HapticTests: **17 passed, 0 failed** (`Logs/Haptics-tests.xml`). Covers accepted pistol shots, eight rejection cases, muzzle obstruction, five automatic rifle bullets with intervening cooldown checks, armor/health/lethal damage, run restoration, OFF suppression without gameplay changes, default/persisted settings, silent UI, Editor no-op, idempotent Android manifest permission, and real accepted enemy pistol/rifle bullets.
- Affected combat/menu regressions: **10 passed, 1 failed** (`Logs/Haptics-regression-tests.xml`). Combat resolution, settings, menu callbacks and idempotent setup on both GamePoc and ConstructionSite passed. The existing paused-camera test fails at `InGameMenuTests.cs:198`: Tactical camera displacement is about 0.03 instead of greater than 1. Repeating that test with the original pre-haptics InGameMenuController reproduces the same failure (`Logs/Haptics-camera-baseline-tests.xml`). The current controller was restored afterward; no camera behavior was changed.

## Remaining device checks

1. Build/install Android and verify the merged APK manifest includes VIBRATE. On OnePlus Nord 3, compare pistol taps, short distinct rifle taps, and stronger armor/health damage. Repeat on hardware without amplitude control if available.
2. Build with Xcode and test a supported physical iPhone; the Objective-C++ source cannot be compiled or exercised on this Windows workstation. Check intensity distinction and first-shot latency after idle/backgrounding. Check an unsupported haptic device/simulator safely stays silent.
3. Toggle OFF, shoot both weapons and take damage, then relaunch/Continue and confirm OFF remains. Re-enable and confirm feedback returns.
4. Check rejected/empty/cooldown/muzzle-blocked fire and Jump/Sprint/Aim/Hoist controls stay silent. Enemy fire stays silent unless it actually damages the player.
5. Review both Options layouts on the phone, including safe areas and progress-reset confirmation blocking. Check sustained automatic fire remains discrete and comfortable; tune only profile assets initially.
