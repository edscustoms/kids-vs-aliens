# Audio System V1

**GAMEPLAY CODE MUST NOT DIRECTLY REFERENCE AUDIOCLIPS**, unless an exception is documented here.

The source of truth is **gameplay semantic event → SoundEvent → clip variants**.
Replacing a clip normally changes one SoundEvent asset, with no gameplay or prefab edits.

## Everyday workflow

1. Open **Tools > Audio > Audio Library**. Search by event, category, description or status.
2. Click **Edit / Select**. Expand **Variants**, drag replacement clips into the slots, and
   edit status, volume/pitch ranges, 2D/3D distances, mixer group and replacement notes.
3. **Preview** auditions a random nonrepeating clip through Unity's Editor preview player.
   It previews the raw clip; use Play Mode to hear event gain/pitch, mixer and 3D attenuation.
   Stop Preview, closing the window or entering Play Mode stops auditioning.
4. To add a sound, choose a category and semantic name, then **Create Event**. The window
   creates the asset under `Events/<category>/`. All SoundEvent assets are shown through
   AssetDatabase, including unused Candidates. Once an event is wired into gameplay,
   open **Runtime Manifest** and add it to the list; unused assets stay out of that list.
5. Run **Tools > Audio > Validate Library**. Warnings include missing/null/duplicate clips,
   null/duplicate manifest entries, missing groups, invalid setup, stereo spatial sounds and wasteful or
   delayed gameplay imports, including Android/iOS overrides. Validation never changes clips.

The starter pack now populates pistol, melee, Beam Start/End and the two shared UI events
as **Placeholder**. Beam_Loop intentionally stays **Missing**. Twelve other events are
unused **Candidates**. See [StarterAudioPack.md](StarterAudioPack.md) for the exact mapping
and locked menu click/launch rules. Nothing in this pack is Final.

## Files and runtime

- `Assets/Game/Audio/AudioLibrary.asset`: runtime/build/preload manifest for wired events only.
- `Assets/Game/Audio/Events/`: semantic SoundEvent assets, including the five proof events.
- `Assets/Game/Audio/Clips/`: recommended home for future source audio.
- `Assets/Game/Audio/Mixers/MainAudioMixer.mixer`: Master → Music, SFX, UI, Ambience, Voice.
  Exposed decibel parameters are `MasterVolume`, `MusicVolume`, `SFXVolume`, `UIVolume`,
  `AmbienceVolume`, `VoiceVolume`; settings UI integration is future work.
- `Assets/Game/Audio/Presets/`: native AudioImporter presets.
- `Assets/Game/Scripts/Audio/`: SoundEvent, AudioLibrary, AudioService, AudioEmitter,
  BeamAudioPresentation, UIAudioFeedback and UIAudioButton.
  `Assets/Game/Editor/Audio/`: authoring/window/validation/review.

AudioService is scene-owned and precreates 32 one-shot AudioSources in Awake. There is
no source creation/destruction, coroutine, clip loading or managed allocation per shot.
The library is registered once at Awake; adding new events during Play requires a restart.
Ownership is acquired on enable and released on disable, so a disabled surviving service
cannot block a replacement. Re-enabling reuses its pool; active duplicates are rejected.
Clip selection and gain/pitch variation use an audio-private PRNG, never UnityEngine.Random.
Replacing variants on an already registered event affects its next playback immediately.
Missing, unindexed or not-yet-loaded gameplay clips fail silently; validate before testing.

Pre-lock cleanup validation: **45/45 Unity tests passed** in `Logs/AudioCleanup-tests.xml`.
This covers the audio/UI/scene-repair suites, full PlayerMeleeTests suite, and focused Beam
phase/transition tests. Regressions check candidate dependency exclusion after pack repair,
Editor visibility, Unity random-state isolation, single emitter startup with cooldown,
disabled-service replacement/re-enable, reentrant/cancelled motion transitions, and confirmed
melee audio only when the resolver returns a receiver. Library validation retains only the
intentional Missing Beam_Loop warning. Both runtime and Editor assemblies compile.

```csharp
// This is a serialized SoundEvent reference, never an AudioClip.
AudioService.Play(fireSound, muzzle.position);
AudioService.Play2D(confirmSound);
```

AudioEmitter owns an existing local AudioSource. Call `Play(sound, true)` for a loop,
`Play(sound, false)` for object-bound one-shot, and `Stop()` to stop/release it. Its Inspector
also supports a default event, loop and play-on-enable. Do not put another AudioSource on
the same emitter object. Disable/destroy stops playback; no fade coroutine is used in V1.
Initial auto-play runs once in Start, after services initialize; later enables play directly.

Event voice caps and cooldown apply across both source types. The service has 32 active
emitter slots by default. When a cap/pool is full the new request is dropped; there is no
voice stealing or growth. Pitch is positive, Doppler is disabled, and distance rolloff is
logarithmic. Gameplay audio pauses with `Time.timeScale == 0`; UI events may opt into
`playDuringPause`. App background pauses every managed voice. Transient sounds are not saved.
Use one active gameplay scene/service at a time; additive multi-scene audio ownership is deferred.
For accepted menu launches, the old service retains only its already-playing UI tail
through scene unload, yields ownership to the next scene and then destroys itself.

## Mobile imports

Select imported clips and apply the preset using the Inspector's Preset selector:

| Preset | Channels | Loading |
| --- | --- | --- |
| SFX_Short_Mono | Mono | Decompress on load; synchronous preload |
| SFX_Loop_3D | Mono | Compressed in memory; synchronous preload |
| Ambience_Stream | Preserve stereo | Streaming/background; for long ambience |
| Music_Stream | Preserve stereo | Streaming/background |
| Voice_Mono | Mono | Compressed in memory; synchronous preload |

Presets use Vorbis at 0.7 quality, optimized sample rate, and matching Android/iOS overrides.
Short decoded SFX trade some RAM for immediate playback; review aggregate memory on device.
Ambience need not be stereo: force mono for a localized emitter. Use an in-memory preset
for short ambience. Import presets are opt-in and never bulk-applied to existing audio.

## Proof integration and scene repair

- **Pistol:** `WeaponItemData.fireSound` is assigned only on PlasmaPistolItem. PlayerShooter
  plays it at the muzzle next to shot VFX, after aim/shot acceptance. Rifle stays unassigned.
- **Melee:** `UnarmedCombatItemData.impactSound` is assigned on Fighting. PlayerMeleeController
  plays at the confirmed contact point after existing physics/LOS checks and hit resolution.
  An animation marker or air swing alone never produces an impact sound.
- **Beam:** BeamAudioPresentation observes BeamTransportController's semantic phase events.
  Start on show/materialization, loop on first travel frame, stop loop + End at destination.
  Arrival landing hold does not extend the loop. Cancellation/disable stops it without End.
  Hoist curve, VFX timings and the automatic arrival/manual pad distinction are unchanged.

Menu, GamePoc and ConstructionSite include the service; ConstructionSite's existing Beam controller
also has its audio observer/emitter. GamePoc's saved scene predates the Beam controller.
For older/new scenes use **Tools > Setup > Setup or Repair Active Gameplay Scene**, then save.
The canonical helper calls AudioSceneSetup after Beam setup, reuses existing components and
references, and preserves assigned sound tuning. Audio assets can separately be recreated
with **Tools > Audio > Create or Repair Audio Assets**. No runtime Editor/asset-search APIs.
The existing **Tools > UI > Setup or Repair Active Menu** command repairs menu audio/listener wiring.

Existing Starter Assets footsteps/landing use legacy sources and clip/container references.
This is the explicit V1 exception: migrating them is outside the three proof integrations,
and the locomotion controller already has unrelated local edits. New gameplay audio uses SoundEvents.

## Validation and remaining checks

Both project C# assemblies compiled against Unity 6000.5.6f1. Isolated Unity Play Mode smoke
review passed immediate playback, pooled identities, shared voice caps, cooldown, variant
replacement, mixer/2D/3D assignment, pause/resume and disable cleanup. A warmed 100-shot loop
allocated **0 managed bytes**. Run via `-executeMethod AudioRuntimeReview.Run` in an isolated
batch Editor (without `-quit`); it exits with pass/fail. Review uses generated silent test clips.

All three `AudioSystemTests`, both `AudioSceneSetupTests` (GamePoc/ConstructionSite), and
the extended Beam audio-phase assertion passed in the full project's Test Runner.
See StarterAudioPack.md for current UI checks and unrelated regression failures.
Logs for executed checks are in `Logs/*Audio*` (local, untracked).

Still test in the full Unity project with real assigned clips:

- Library preview, replacing a clip, and audible mixer faders.
- Pistol rapid fire/reload/blocked shots, rifle regression; melee hits vs air swings/cover.
- Hoist/arrival/departure start-loop-end, cancel, pause mid-loop and scene unload.
- Rerun canonical scene repair twice in GamePoc and ConstructionSite; verify no duplicates.
- Android first-shot latency, looping, background/lock/resume, voice saturation and memory.

Final sound selection, legacy footsteps, other weapons/enemies/machinery, volume UI,
music systems and occlusion are deliberately deferred.

Native mixer creation and Editor audition use small isolated reflection calls because Unity
does not expose public equivalents; signatures were checked in the installed 6.5 Editor and
against [Unity's reference source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Audio/Mixer/Bindings/AudioMixerController.cs).
