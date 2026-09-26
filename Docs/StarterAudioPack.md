# Starter audio pack

18 supplied WAVs were moved using Unity's AssetDatabase (GUIDs checked across each move).
All live assignments are **Placeholder**; unused choices are **Candidate**. Nothing is Final.
Edit/replace them in **Tools > Audio > Audio Library**, never on individual buttons.

## Live assignments

Paths below are relative to `Assets/Game/Audio/Clips/`.

| SoundEvent | WAV | Use |
| --- | --- | --- |
| UI_Click | UI/cool-interface-click-tone-2568.wav | Menu/navigation and inventory/quick-slot selection |
| UI_PlayConfirm | UI/click-melodic-tone-1129.wav | Accepted final launch, Continue, Resume or confirmed restart |
| Weapon_PlasmaPistol_Fire | Weapons/short-laser-gun-shot-1670.wav | Actual pistol emission |
| Combat_Melee_Impact | Combat/game-whip-shot-1512.wav | Confirmed contact; stylized snap pending a proper body-impact replacement |
| Beam_Start | Beam/mixkit-alien-technology-button-3118.wav | Short temporary materialization/activation cue |
| Beam_End | Beam/mixkit-sci-fi-tube-swoosh-912.wav | Temporary energy-release cue on reaching the destination |

**Beam_Loop stays Missing.** The sustained reactor sample is not certified seamless and
is reserved for machinery evaluation; no arbitrary repeating beam sound was forced in.

## Editor-only candidates with no runtime trigger

These files are deliberately unused in gameplay. Their SoundEvents make later audition
and replacement easy without adding new integrations now.
They are excluded from AudioLibrary, the runtime/build/preload manifest. The Editor window
still discovers them via AssetDatabase. The manifest holds only the six live assignments
and the wired, intentionally Missing Beam_Loop event. Pack repair does not index candidates.

| Candidate SoundEvent | WAV |
| --- | --- |
| Machinery_Reactor_Buzz | Machinery/mixkit-electricity-reactor-buzz-904.wav |
| Machinery_TechSwitch | Machinery/mixkit-sci-fi-click-900.wav |
| Machinery_Drill_Alert | Machinery/mixkit-sci-fi-drill-alert-760.wav |
| Machinery_Robot_Interaction | Machinery/mixkit-sci-fi-interface-robot-click-901.wav |
| Machinery_Mechanical_Alert | Machinery/mixkit-synth-mechanical-notification-or-alert-650.wav |
| Alien_Vehicle_Flyby | Aliens/mixkit-flying-space-bike-1612.wav |
| Environment_TechDoor_Open | Environment/mixkit-futuristic-door-opening-906.wav |
| Weapon_Plasma_Charge | Weapons/mixkit-sci-fi-plasma-gun-power-1680.wav |
| UI_HintNotification | UI/mixkit-interface-hint-notification-911.wav |
| UI_Notification_Success | UI/mixkit-sci-fi-confirmation-914.wav |
| UI_Scanner_Zoom | UI/mixkit-sci-fi-interface-zoom-890.wav |
| UI_Tech_Transition | UI/mixkit-tech-transitions-3176.wav |

## Locked menu rule

UIAudioFeedback holds the two shared SoundEvent references on each scene's AudioService.
UIAudioButton observes the existing uGUI onClick event, covering mouse, touch and Submit.
A one-time scene pass covers authored/inactive screens; InterfaceFactory and UIButton
register dynamically created buttons. Custom inventory slot selection requests the same click.
The centralized hook excludes gameplay input controls identified by `UIVirtualButton`,
`UIVirtualJoystick`, `UIVirtualTouchZone` or `BeamHoistButton` on the button or its parents.
Jump/Hoist, Fire/Aim and Sprint therefore stay silent without per-button sound overrides.
The exclusion also runs when a click arrives, covering existing hooks or input components
added after registration. It does not suppress the surrounding HUD's navigation or inventory.
No filenames, AudioClips, button-label comparisons or save-state guesses live in button code.

Ordinary clicks are dispatched in LateUpdate. An accepted final action in that frame
replaces the pending click with UI_PlayConfirm, regardless of listener order. Both events
are 2D, UI-routed and playable while paused.

| Action | Sound |
| --- | --- |
| Play with no active run, successful launch | UI_PlayConfirm |
| Play opens Active Run Found | UI_Click |
| New Game opens replacement confirmation | UI_Click |
| Cancel / Back / Options / Knowledge / ordinary navigation | UI_Click |
| Confirm New Game, successful launch | UI_PlayConfirm |
| Continue, accepted saved-run load | UI_PlayConfirm |
| Failed launch/Continue | UI_Click; no false confirmation |
| Pause/Inventory Resume, actually releases suspension | UI_PlayConfirm |

The final tone is started before the old scene unloads. AudioService temporarily retains
its already allocated UI sources, relinquishes singleton ownership to the next scene,
stops old gameplay/loops and destroys the retired pool after the tail (5-second safety cap).
Playback requests do not load clips or create per-button sources, and never delay gameplay transitions.
Successful Quit/reset/recovery transitions back to Menu retain the ordinary UI_Click tail.

Menu, GamePoc and ConstructionSite are wired. Menu gained a listener on its display camera.
GameplaySceneSetup and the existing menu repair command both repair this audio dependency.

## Imports and validation

All short clips use the existing **SFX_Short_Mono** preset: mono, Vorbis 0.7,
optimized sample rate, synchronous preload/decompress-on-load, Android/iOS overrides.
The 12.06-second reactor and 5.58-second charge use **SFX_Loop_3D** for compressed-in-memory
playback with synchronous preload. Neither preset implies a runtime loop; these are unused
candidates. No timing-critical clip streams; original WAV content was not edited.

The starter-pack assignment and scene wiring are already authored. The one-off pack
organizer has been retired. Edit SoundEvent assignments through **Tools > Audio > Audio
Library**, then use **Tools > Audio > Validate Library**. Canonical gameplay/menu repair
maintains required scene wiring; it does not reassign the pack.

Library validation: **one intentional warning**, missing Beam_Loop. No avoidable clip,
index, mixer or importer warnings. Both assemblies compile.

Manual acceptance remains: audition Library previews and both menu paths (with/without
an active run), pause/inventory navigation, rapid pistol fire and real melee contacts,
Beam start/end, and Android latency/background/resume. Sound quality is provisional.

The nine focused audio tests passed: imports/locked assignments, native assets, repair
idempotence on GamePoc and ConstructionSite, variant selection, missing-clip validation,
authored HUD input exclusions on both scenes, and Play Mode button policy (silent action
controls including existing hooks and late-added input roles, preserved input callbacks,
inventory/quick-slot clicks, paused clicks, both listener orders, disabled buttons, real
scene unload, retained tail and pool cleanup). The broad run also passed the current
Beam audio-phase test, scene-start flow and procedural UI tests.

Four broader regression failures remain: Beam ability-limit/legacy-phase/prefab expectations
and the paused camera image-difference test; those gameplay/visual systems were not changed
in this starter-pack task. The broad run also recorded an audio test closure lost on domain
reload; that fixture was corrected and its focused rerun passed.
Logs: `Logs/StarterAudio-tests.xml`, `Logs/UIAudio-tests.xml` and `Logs/HudAudio-tests.xml`.
