# AGENTS.md - Kids VS Aliens

These rules apply to all repository work. Human readability and obvious ownership are
first-class architecture requirements. A competent Unity developer should be able to
find the owner, understand the behavior, add content and identify the relevant tests
without reconstructing the project through an AI.

## Engineering philosophy

Build simple, readable, obvious code: boring where possible, easy to debug, extend and
onboard into, with shallow indirection and explicit state ownership. Prefer a
straightforward component, clear data and prefab/configuration plus small isolated
behavior over a chain of frameworks, adapters, factories and events.

- Use composition for shared capabilities. Reuse existing combat, perception, item and
  presentation systems before adding another implementation.
- Avoid unnecessary DI frameworks, deep inheritance, excessive interfaces, factories
  without a concrete need, reflection-heavy runtime systems, invisible event chains,
  generalized managers and speculative abstractions.
- An abstraction must solve a real repeated problem. Explain its concrete benefit;
  SOLID or design-pattern terminology alone is not justification.
- Human complexity is architecture debt. A correct action that requires tracing eight
  classes, five events and hidden reflection can still be a maintainability regression.
  Before adding indirection, ask whether it makes the next developer's job easier.
  Prefer direct dependencies when ownership is clear.
- Fix root causes with the smallest clear change. Do not silently redesign neighboring
  systems, add unrelated cleanup or build architecture for hypothetical requirements.

## Sources and accepted baseline

Read [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md) and [TODO.md](TODO.md) before substantial
implementation. [RepositoryAuditRemediation.md](Docs/RepositoryAuditRemediation.md)
is the authoritative post-audit baseline: no verified Critical/High architecture
problems, requested remediation complete, Full Editor regression **318/318 passed**.
That is recorded baseline evidence, not a permanent test-count requirement or proof
that later changes are correct.

The explicit current task takes precedence. Inspect current code/assets for implementation
facts; use the remediation report for accepted post-audit contracts. Report discrepancies
rather than silently following stale documentation or treating an accidental behavior as
intent. TODO governs priority, current PROJECT_CONTEXT overrides supersede its historical
notes, and historical plans are not evidence that a system exists.

The project uses Unity 6.5 and URP; `ProjectSettings/ProjectVersion.txt` supplies the exact
Editor version. Android/iOS are first-class targets; desktop remains supported.

## Ownership before code

Determine who owns the state before implementing behavior. There should normally be one
obvious owner. Do not copy logical state into another system without an explicit, necessary
synchronization contract. If two systems appear to own it, stop and inspect before adding
another copy.

| Concept | Current owner and extension entry point |
| --- | --- |
| Inventory | `PlayerInventory`: possession, quantities/stacks, quick-slot assignments, Plasma/Armor Capsule quantities, reload payment, unique weapons and one run-specific `OwnedWeaponState` per owned weapon definition, including magazine/reload/cooldown state. |
| Equipment | `PlayerEquipment`: selected owned weapon and mounted presentation. `WeaponInstance` represents the mounted weapon; it does not own its logical magazine. |
| Firing/combat | `PlayerShooter` operates on active owned state and owns accepted delayed player hits. Reuse `HitInfo`, `CombatHitResolver`, `IDamageable` and `IHitReaction`. |
| Enemies | `EnemyActor`, `EnemyBrain` and shared movement/perception/combat components; `EnemyEquipment` owns enemy equipment. Compose prefabs with combat/animation profiles and loadout data, independently of the replaceable visual model. |
| Beam | `BeamHoistSurface`/baker own authored valid cells; `BeamHoistAbility` supplies reach constraints; transport gameplay validates and moves; presentation observes. |
| Camera | `GameplayCameraController`/presets own framing; `CameraOcclusionController` owns fade/visibility state; silhouettes consume it; camera-feedback service/profiles own render-only feedback. |
| Audio | Gameplay requests a semantic `SoundEvent`, not arbitrary clip references; event/library configuration owns clip variants and `AudioService` owns playback. Weapon fire sound belongs in weapon data where appropriate. |
| Haptics | `HapticService`, profiles and platform backends. Local-player feedback must not fire merely because an enemy shoots. |
| Knowledge/permanent data | Books/`SkillData` define capability unlocks; `PlayerSkillState` exposes player skills; `PermanentProgress` persists learned Knowledge, XP and acknowledgement state. |
| Active Run | `ActiveRunController` coordinates the current attempt/world/player snapshot with `RunSaveService`. `RunWorldObject` supplies stable identity; participants own their component state. |
| Suspension | `GameplaySuspensionController` owns suspension leases and world-pause policy. Consumers acquire/release leases rather than independently restoring global pause state. |
| Authored encounters/triggers | `AuthoredEncounter` activates dormant enemy members; `GameplayTrigger` owns entry/conditions/remembered one-shot state. Their optional `GameplayActions` call existing owners directly. Use PF_AuthoredEncounter/PF_GameplayTrigger and stable RunWorldObject identities. |
| Objectives/dialogue | `ObjectiveController` owns Active Run objective state/progress; `DialoguePlayer` owns transient ordered speech and semantic voice. CharacterVisual/DialogueSpeaker owns speaker identity, DialogueMessage owns content. GameplayCommunicationView presents; GameplayMessageLayout only reserves space. See Docs/GameplayAuthoring.md. |
| Healing Pod | `HealingPodController` owns the sequence and remaining run-specific healing capacity in full-health-bar units. Prefab markers/triggers survive visual replacement; `PlayerHealth` owns grants, `PlayerAnimation` owns temporary falling presentation, suspension leases own control, and Active Run records the reserved safe exit. Preserve authored prefab/instance tuning. |
| Alien bikes | `AlienBikeController` owns ride physics/resources and the last safe grounded save pose; `PlayerBikeRider` owns the player mount/seat/dismount sequence through suspension leases and PlayerAnimation. Rideable-wrapper `AlienBikeVisualFeedback` owns cosmetic lean/control rotation; PlayerAnimation applies its temporary rider offset. `AlienFlybyController` owns a separate cosmetic pool on validated `AlienFlybyPath` routes. Both compose PF_AlienBikeVisual; no ride/path state belongs on the model. See Docs/GameplayAuthoring.md. |
| UI/previews | Edit the actual screen owner/runtime builder. See the [screen ownership map](Docs/ProceduralUI.md#screen-ownership-where-to-edit); generated controls and hidden legacy scene controls are not interchangeable. Preview wrappers/settings must not force gameplay-prefab changes. |

Do not create a second system for a concept the repository already owns. If a requested
feature conflicts with an established boundary, pause the affected implementation,
report the conflict and propose the smallest options before proceeding.

## Approved behavior that must survive changes

Distinguish a bug from unusual but intentional design. Inspect docs/tests/history when
intent is unclear. Movement, touch/desktop input, aim/target switching, muzzle safety,
weapons, grenades, Knowledge gating, previews, enemy perception/LOS and fading are
production-sensitive; inspect their consumers before changing shared code.

### Weapons and delayed plasma hits

- Combat Economy V1 uses full-magazine, empty-only automatic reloads. Inventory pays the
  weapon-authored Plasma Capsule cost once at start; paid timers survive swaps/Continue.
  Only world pickup acquisition converts duplicate plasma weapons; restore/ensure-owned
  never grants rewards. EnemyPlasmaLoot resolves separately before equipment returns its
  original scavenged pickup. Armor Capsules remain separate; no consumption mechanic exists.
- Use `WeaponItemData`, equipped/world prefabs, `WeaponInstance`, GripPoint/Muzzle,
  required skill, fire mode and animation style. Inspect inventory stack/uniqueness
  rules before changing acquisition, quantities or consumption.
- Reselection and swaps must not refill magazines, recreate owned state, cancel reload
  or reset cooldown. Active Run preserves all owned magazines/timers, not just selected
  ammo. Retain the documented legacy/missing-record fallback without a migration framework.
- `EquippedWeaponChanged` means actual selection/unequip. Temporary hiding/style uses
  `WeaponPresentationChanged`; never overload equipment-change signals for visibility.
- Approved player plasma flow: **accept shot -> resolve physics target/point -> gameplay
  owns pending hit -> visual travels -> scheduled arrival commits damage, reaction and
  impact together**. Share travel timing with the visual. Releasing/disabling/destroying
  cosmetic VFX must not cancel accepted damage; missing VFX must not cause instant damage.
  Preserve world-pause and Beam input-suspension timing. Do not convert this into instant
  hits or physical-projectile gameplay merely to simplify ownership.
- Physics remains authoritative for hits, cover, LOS, muzzle obstruction, projectile
  interception and grenade collision. Query ordering is unspecified; preserve nearest
  eligible target/blocker rules, masks, body samples, low-cover and intervening-alien rules.
- Tune one weapon through its data/prefab overrides; do not change shared `PlasmaCore`
  just for that weapon. Reuse the grenade foundation and pool-friendly effects.

### Beam, animation and camera

- Manual hoist uses Jump with Knowledge and gameplay eligibility. Preserve materialization
  and one continuous cubic Bezier from the true start to landing. Control points shape
  that curve; do not add a vertical phase, second curve, reset or teleport. Preserve the
  established beam following, glow/flicker and separation of automatic level arrival
  from the manual hoist-start pad.
- Gameplay and presentation share baked valid cells. Preserve exact footprints, holes
  and disconnected pockets; merging visuals must not fill invalid space. No runtime
  baker reshapes the area around the player. Movement changes visibility/state, not the
  authored footprint. Reach constraints come from `BeamHoistAbility`.
- Gameplay decides what happens; semantic animation actions/mappings decide how it looks.
  Grenade release and melee impact use the existing typed authored animation markers,
  not magic gameplay delays. Keep floating/transport logic independent of concrete clip
  or Animator state names. Prefer import/mapping fixes over runtime rotation hacks.
- Keep framing, occlusion, silhouettes and render-only feedback separate. Feedback/recoil
  must not alter gameplay aim/raycast transforms.
- Preserve current occlusion: ten context rays plus five body samples, authored thresholds
  and logical groups, active authoritative colliders, and existing faded-blocker aim/LOS
  semantics. Membership/material/group-height caches are built at startup; runtime-added
  or reparented blockers are unsupported. Do not document dynamic support as implemented.
- Occlusion material copies are lazy, reused and cleaned up; do not replace this with a
  blanket prohibition on copies or introduce per-frame instantiation. `Active` ownership
  must survive reenable and fall back to a surviving enabled controller after duplicate
  teardown. For any runtime material copies, make ownership/cleanup explicit;
  `EnemyDeathSequence` destroys only its own copies, never shared project materials.

## Persistence: mandatory participant contract

Keep Active Run and Permanent Progress separate. Run-specific inventory/world/player
state belongs to the attempt; learned Knowledge, XP and acknowledgement persist across
run resets. Ordinary Quit, backgrounding, calls, lock/unlock and recoverable termination
are suspend/resume, not death. Continue restores functional state and returns paused.
Death, confirmed Hard Restart and confirmed New Game replacement discard active state;
permanent reset is a separate explicit action. Transient projectiles/animation frames
need not be reconstructed.

Current restoration intentionally applies world state twice, with player restoration
and another frame between passes. Every new `IRunStateParticipant` must:

- Use a stable key unique within its `RunWorldObject` and stable identities for peers.
- Treat `RestoreRunState` as absolute, idempotent state assignment; assume two calls.
- Never grant rewards, consume resources because an action was already completed, or
  replay objective-completion effects during restore.
- Expect `OnEnable` before restored state and peer identities before peers have final state.
- Defer peer-dependent decisions until restoration completes / `ActiveRunController.IsReady`;
  do not depend on iteration order.

These rules apply to future objectives, doors, machinery, excavator repair, set pieces
and persistent spawns. Read the interface XML and [persistence contract](Docs/RunInterface.md).
Test a real Save/Continue flow, including cross-object state and absence of duplicate
side effects when relevant. Do not add a restore dependency graph or released-save
migration framework without a demonstrated need; current compatibility is pre-release.

## Extension rule: how do I add the next one?

Known growth should normally mean data + configured prefab + authored content, with one
small isolated behavior when genuinely unique. It should not require five central edits,
new switch statements, unrelated UI changes and manual persistence patches.

Inspect the existing owner first: `SoundEvent`/`WeaponItemData` for weapon sound,
`PlayerInventory`/`OwnedWeaponState` for weapon state or quantities, `PlayerEquipment`
for selection, camera-feedback profiles/service for impacts, modern enemy components
and profiles for AI, and Active Run/`RunWorldObject`/`IRunStateParticipant` for world saves.
Use the UI ownership map before editing a screen.

Prefer ScriptableObjects/profiles/prefabs/serialized configuration for weapons, enemy
archetypes, animation sets, sounds, haptics, camera feedback, items, grenades and loot.
Use the same principle for difficulty configuration when that system exists. Do not
force genuinely behavioral logic into data just to avoid writing a small component.

Keep blast radius related to the feature. Grenade stacking can touch inventory,
consumption, quick slots, count UI and persistence; it should not unexpectedly require
Beam, camera, navigation or scene-loading changes. Investigate before spreading a small
feature across unrelated systems.

## Scene, asset and repair safety

Prefer explicit serialized references or established authoring contracts over hardcoded
scene names, arbitrary hierarchy names, broad Find calls, hidden scene scans and child
ordering. Scene names are acceptable when scene identity itself is intended data.
Preserve documented UI/authoring name contracts without spreading them elsewhere.

The canonical command is **Tools > Setup > Setup or Repair Active Gameplay Scene**.
When adding a required shared scene/player dependency, extend `GameplaySceneSetup` in
that same task, reusing feature helpers. Standard dependencies must not rely on
undocumented manual wiring; level-specific content remains explicit authoring.

- Repair ensures required dependencies, preserves valid extra references/order and
  authored tuning, deduplicates entries, and remains idempotent. Inspector values may
  intentionally differ from C# defaults. Routine repair must not retune import settings.
- Preflight known fatal ambiguities before mutation where practical. Shared asset/import
  writes are not scene Undo; errors do not guarantee rollback. Prefer saving the specific
  changed assets over broad `AssetDatabase.SaveAssets()`. Separate explicit migrations
  from routine repair; do not build a transaction framework.
- Test preservation and a second repair run using disposable fixtures/copies. When
  changing shared wiring, check GamePoc and another gameplay scene where practical.
  The audited GamePoc/ConstructionSite repeat-repair baseline had no file churn.
- Preserve uncommitted work, Unity GUIDs, metadata and serialized references. Review
  every changed scene, prefab, ScriptableObject, importer, renderer and ProjectSettings
  file for intentional changes. Avoid mass YAML normalization; use Editor-safe changes
  when text editing is risky. State Inspector-only uncertainty instead of guessing.
- Change large art/models/textures only when the task requires it. Keep `.meta` files intact and
  tracked; verify LFS binaries rather than mistaking pointer files for corrupt assets.

## Testing and evidence

Use the [Tests README](Assets/Game/Tests/README.md) for supported commands and isolation.
From the repository root in PowerShell with Unity closed:

```powershell
.\Tools\Run-UnityTests.ps1 -Suite Quick -ReuseCopy
.\Tools\Run-UnityCoreTests.ps1
.\Tools\Run-UnityTests.ps1 -Suite Full -ReuseCopy
# Focused selection overrides the suite filter:
.\Tools\Run-UnityTests.ps1 -Suite Full -ReuseCopy -TestFilter 'OwnedWeaponStateTests;AuditContinueTests'
```

Quick runs fast focused contracts; Core is a **partial category**, never full regression.
Full runs every Editor test with graphics enabled. Do not use `-nographics` for render/URP
verification. The runner uses a physical isolated project copy, isolated saves/PlayerPrefs
and explicit XML/log paths under `Logs/RepositoryAuditRemediation`; `-ResultName` selects
a unique output name. Never substitute a historical test count for current results.

- Run appropriate focused tests during iteration and Full at meaningful integration
  checkpoints; do not demand the longest suite for every tiny edit. Compile code changes;
  a Roslyn check alone does not prove Unity import, rendering or lifecycle behavior.
- Tests must work from a clean checkout without ignored migration logs as oracles.
  Ordinary tests must not mutate production assets; authoring/import tests use disposable
  fixtures with deterministic cleanup and isolated saves/output.
- Assert required membership, uniqueness, exclusions and mappings instead of arbitrary
  content totals. Keep legitimate scene-specific count/name acceptance tests separate
  from reusable architecture assumptions. UI tests target visible, interactable controls.
- Classify failures: pass-caused regression, pre-existing production failure, stale test,
  or environment/runner failure. Fix regressions; never alter correct gameplay just to
  satisfy an obsolete test. Report failures and limitations rather than hiding them.

## Mobile-first implementation

Consider touch, screen space/readability, Android/iOS backgrounding, calls, lock/unlock,
OS termination, thermal limits, allocations and GPU cost. Avoid desktop-only gameplay
assumptions, unnecessary Update work/allocations, repeated broad searches/loads and
uncontrolled Instantiate/Destroy loops. Profile before speculative optimization.

Editor tests do not prove device lifecycle, touch feel, native behavior, performance or
GPU-driver rendering parity. Current follow-ups remain Android lifecycle/input, the
separate Pixel rendering issue, Realme X2 save/Beam profiling, and Mac/iOS build/haptics
validation. Report those as pending, not as covered by Editor results.

## Naming, placement and legacy code

No `KVA` prefix in public classes, tools, shaders, materials, menu paths, namespaces or
filenames. Use descriptive names; rename declarations, callers and lookups atomically,
preserving serialized/native bindings. Do not make unrelated mass renames.

Put new code with its logical system, Editor-only code under Editor, tests in the
established test tree, and data/assets in the nearest established content area. Historical
folder inconsistency does not justify a mass move. Consider a small safe move during
related work only when discoverability materially improves without unnecessary churn.

Never infer dead code from V1/old/legacy names, a missing obvious caller or one GUID search.
Before deletion inspect scenes, prefabs, tools, reflection, build preprocessing, catalogs,
tests, migrations, development sandboxes and relevant old-save implications. Dead-code
removal is deliberate, separately scoped and independently validated.

## Working discipline and documentation

Before editing: understand the request, identify owners, inspect actual scripts/data/
references/tests, trace consumers, reuse existing capabilities and identify persistence/
device implications. Do not guess class names, fields, assets or enums. Keep the requested
scope and authored decisions intact. If the work becomes substantially larger because
an ownership boundary cannot support it, stop the affected implementation, explain the
problem and propose the smallest options; do not quietly build a framework.

After implementation: compile and test as appropriate, review the entire diff for unrelated
changes, run `git diff --check`, and report changed files, ownership decisions, exact test
results and remaining manual/device checks. Distinguish implemented, code-reviewed,
Unity-tested and device-tested. Give a short relevant checklist, not generic assurances.
Documentation-only work needs document/diff validation, not a claimed new Unity test run.

Keep docs small: ownership, non-obvious lifecycle reasons, extension workflows, dangerous
tool behavior and supported commands. Prefer updating the existing relevant document
and comments explaining reasons/contracts over duplicated status documents, obvious C#
commentary or sprawling architecture essays.

Difficulty/Madness, further mission content beyond the excavator repair and Bike-level content are upcoming work,
not current architecture. Implement them only within an explicit task; apply these same
ownership/readability rules and update this file with durable contracts when established.
Keep temporary balance values, feature TODOs, animation/IK wish lists and roadmap history
in their existing design/workflow documents rather than expanding this constitution.
