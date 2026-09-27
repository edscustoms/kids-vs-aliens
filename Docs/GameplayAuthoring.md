# Gameplay authoring V1

ConstructionSite uses two reusable prefabs under `Assets/Game/Prefabs/Environment`.
Gameplay state remains with its existing owners; neither prefab owns combat, input,
camera or a second save system.

## Encounters

Drag `PF_AuthoredEncounter` into a gameplay scene. Select its BoxCollider and use
**Edit Collider** to fit the trigger. Move/rotate the inactive enemy children in
Scene view. The template contains three reusable melee enemies; edit the explicit
Enemies list when removing members, or use **Add dormant enemy member** with an
existing enemy prefab/archetype. Placement is the actual enemy Transform.

The encounter Inspector exposes each member's existing `EnemyEquipment` combat
profile and starting weapon. None means unarmed; choose an existing ranged-enabled
profile such as `AlienCombatV1` for a Plasma Pistol/Rifle. A melee-only profile
intentionally rejects guns. Equipment, scavenging and loot remain profile-owned.
Optional activation actions default to None. Completion remains derived from the
members' existing enemy/world state; no objective or reward is automatically added.

`Encounter01_FirstMelee` is now an instance of this prefab. Its working trigger,
three melee/unarmed members, authored poses, tuning and four stable world identities
are preserved. No encounter-specific combat or spawning code was added.

## Dialogue and objectives

The scene's `GameplayCommunication` root has two independent owners:

- `DialoguePlayer`: one bounded speech queue and one semantic AudioEmitter. Lines
  run in order on scaled time, with optional gaps. It never acquires suspension or
  controls movement, combat or the camera. Disable/scene exit stops voice and clears
  transient dialogue; Continue does not replay it.
- `ObjectiveController`: one active tracker plus retained inactive/completed/progress
  records. Count objectives complete at their target. Starting another objective
  deactivates the previous tracker; completed records remain available to conditions.

CharacterVisual references a `DialogueSpeaker` asset containing display name,
Boy/Girl voice type and optional stable special-character ID. Message content lives
in `DialogueMessage`, created through **Create > Gameplay > Dialogue Message**.
Exact special-ID matches win; otherwise the speaker's Boy/Girl variant is used.
Each variant owns sequential text, optional SoundEvent, duration and following gap.
Keep individual subtitle lines short; split longer speech into multiple lines.

Audio None works without further setup. Later voice files belong to Voice-category,
2D SoundEvents registered in AudioLibrary, referenced directly by the resolved line.
Duration zero uses actual voice length/pitch plus padding, or reading time when
silent; positive duration overrides it. Voice uses existing AudioService policies.
No trigger plays raw clips, and no TTS or source animation is involved.

Put content in `Assets/Game/Dialogue/Shared` or `ConstructionSite`; name messages
`<LEVEL>_<WHEN/OBJECT>_<PURPOSE>`. The Inspector reference opens the exact text/audio
asset. `CS_Intro_FindWayOut` is an editable sample with no voice, not an extra live
trigger. Amy and Granny have explicit speaker assets on their visual prefabs.

Create objectives through **Create > Gameplay > Objective** under
`Assets/Game/Data/Objectives`. Configure ID, title, None/Count and target count.
The author-facing ID describes the objective; existing catalog GUIDs resolve its
asset in saves. `CS_FindWayOut` is the real ConstructionSite opening objective.
`ActiveRunController.Start` initializes it only in the fresh-attempt branch, after
starting loadout setup. Continue restores state and skips this initializer.

## Triggers and direct calls

Drag `PF_GameplayTrigger`, move/resize its nonblocking BoxCollider, then configure:

- OneShot or Repeatable (once per visit, not every physics frame).
- Optional required objective/state and Knowledge.
- Optional Dialogue, Objective Start/Update/Complete and existing system message.
- Remember One Shot for Active Run persistence.

All None/empty combinations are valid. Player root entry is required, and activation
waits for Active Run readiness and rejects suspended/dead players. Conditions may
become satisfied while inside. Repeatable visits rearm after leaving the volume.
`GameplayActions.Execute(player)` is the same small Inspector block used by encounter
activation; gameplay owners can also call `DialoguePlayer.Play`,
`ObjectiveController.StartObjective/AddProgress/CompleteObjective` directly.

Scene identities are assigned when inspecting these prefab instances and by
**Tools > Setup > Setup or Repair Active Gameplay Scene**. Prefab assets retain blank
scene identities. Run canonical repair after adding content to validate IDs and
register new objective assets in RunContentCatalog. Repair fills missing communication
dependencies/references while preserving authored values. Ambiguous duplicate owners
fail preflight rather than silently selecting one. The completed one-off migration
recipe was removed; routine repair does not regenerate Encounter01 or content assets.

## Presentation and persistence

`GameplayCommunicationView` presents objective and CC independently.
`GameplayMessageLayout` only reserves regions inside the existing SafeArea: objective
at top-center, existing run/system feedback below it, dialogue above quick slots.
Existing feedback scheduling and message behavior are unchanged. New panels ignore
raycasts. TMP uses the project's SIL-OFL Liberation Sans SDF with a shared subtle
outline material, bold objective/speaker and regular transcript.

The `objectives` participant persists objective states/progress on the separate
communication root. `gameplay-trigger` saves remembered one-shot use on its own
stable world object. Both restore absolutely on either world pass without actions,
voice or rewards. Enemy persistence is unchanged. Fresh runs/Hard Restart reload
authored unused triggers and activate the configured opening objective.

Focused fixtures: `GameplayAuthoringTests`, `GameplayCommunicationPlayTests`, plus
existing `AuthoredEncounterTests`/`AuthoredEncounterPlayTests`. These exercise real
Save/Continue, hard restart, conditions, resolution, semantic voice, nonblocking CC,
repeatable entry, repair and simultaneous channels at 1600x720, 1280x720 and 1024x768
with simulated safe insets. Device subtitle readability, touch ergonomics, final
voice listening and native lifecycle behavior still require manual checks.

Unity 6000.5.6f1: `GameplayAuthoring-02` passed **12/12** focused checks.
`GameplayAuthoring-Full` passed **396/397**, including all new/adjacent authoring,
encounter, persistence, Healing Pod, Inventory, LevelStart, repair and audio checks.
The only failure was the existing isolated ranged-combat fixture's three-second
reload/fire deadline (`AlienCombatantTests.LowCoverReloadAndWorldState`). It passed
unchanged in `GameplayAuthoring-ReloadCheck` (**1/1**); the full run is therefore
recorded with a timing-sensitive failure, not as an entirely green run. Combat code
and tuning were not changed. Initial new-fixture failures involved natural trigger
activation preceding an explicit test call and querying a cleared singleton after
disable; both test fixtures were corrected before the focused pass.

XML/logs are under `Logs/RepositoryAuditRemediation`; reviewed UI captures are in
`Logs/GameplayAuthoringV1/communication-*.png`. Scene review preserved 2,487 existing
records verbatim; changes were limited to the encounter prefab connection and new
communication wiring. `git diff --check` passed. No device deployment was performed.
