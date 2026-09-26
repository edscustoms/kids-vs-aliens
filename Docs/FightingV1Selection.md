# Fighting V1 — Motifect selection and validation

Historical validation results and changed-file lists below describe their original passes. Some one-off helpers have since been retired; see [helper cleanup](LegacyHelperCleanup.md). Current automated validation uses the [Tests README](../Assets/Game/Tests/README.md).

19 September 2026. Implementation and Unity validation report.

The production chain is **left jab → right cross → linked hook → heavy rising strike → front kick**. Each step still requires a deliberate FIRE press. The hook and heavy strike are separate derivatives of one authored combination, split at a shared linking pose. The complete 40-FBX source pack remains in `Assets/Game/Animations/Combat_v2/`; no source binary was moved, deleted or edited.

This is an implemented, tested animation candidate, not a certification of finished-game animation quality. The remaining artistic acceptance and reaction limitations are explicit below.

## Selected content and timing

All times are seconds. Contact is an authored typed `MeleeImpact` event in the derivative clip. Recovery is a separate normalized animation binding value. The table below records source ranges and speed; the resulting clips live in `Assets/Game/Animations/Combat_v2/Fighting/`.

| Source | Derivative / semantic role | Source range | Playback speed | Contact after start | Earliest next step |
|---|---|---:|---:|---:|---:|
| `jab_left` | `Light1` / `MeleeLight1`, quick opener | .12–.85 | 1.20× | .233 | .442 |
| `cross_right` | `Light2` / `MeleeLight2`, second strike | .34–1.27 | 1.25× | .352 | .576 |
| `combo_hook_uppercut` | `Light3` / `MeleeLight3`, linking hook | 1.15–1.95 | 1.15× | .391 | .609 |
| `combo_hook_uppercut` | `Heavy` / `MeleeHeavy`, heavy rising follow-up | 1.85–2.85 | 1.20× | .250 | .667 |
| `front_kick` | `Kick`, default fifth step | .55–1.90 | 1.20× | .542 | .917 |
| `roundhouse_kick_right` | `HeavyKick`, mapped alternate; outside default chain | 1.05–2.90 | 1.25× | .560 | 1.200 |
| `jab_left` | `Guard`, looping Fighting stance | 1.15–2.85 | 1.00× | — | — |

The source file name for the heavy strike is not a gameplay contract. On Amy its silhouette is a rising, turning strike; the semantic action remains `MeleeHeavy`. The alternate kick is available through the authored chain data, without a new button or input binding.

The first pass used standalone `hook_left` and `uppercut_right`. Complete sequence renders exposed a narrow-footed reset in the hook and a less coherent heavy follow-up. Those two sources were removed from the production mappings. The two halves of `combo_hook_uppercut` share source time **1.85 s** at the queue handoff, retaining the original connecting motion rather than returning to guard between them.

## Stance, locomotion and transitions

- The pack has no dedicated exploration/combat entry, exit, or directional locomotion set. The existing locomotion clips and blend-tree movement positions/speeds remain in use.
- The shared `CombatStance` parameter still expresses gameplay intent. Exploration ↔ Fighting base transitions use fixed .25 s blends. An upper-body `CombatGuard` layer fades over .25 s while moving.
- `UnarmedCombatActions` retains an upper-body mask for moving punches. Head motion follows the strike, but legs and root remain masked out. Foot IK is disabled on this upper-body layer to prevent it from writing over locomotion feet.
- The independent `CombatFootwork` layer supplies full-body guard/attacks while standing and full-body kick motion during the committed kick phase. It uses Humanoid foot goals. This corrected the controller's narrower guard pose relative to the actual source clip.
- Attack entries and queued transitions blend over .12 s. Recovery returns to the full-body guard over .16 s. Cancellation blends out the pending semantic action and resets footwork to guard before fading it away.
- A grounded moving kick eases horizontal speed to zero through the existing controller acceleration path until its authored recovery gate. Held movement resumes afterward. Punches retain movement. This is the only player-controller change; input values, gravity, collision, facing and root movement remain authoritative as before.

`PlayerMeleeController` now tracks recovery separately from waiting for impact. One queued press can start the next semantic action only after impact and the configured recovery gate. Extra taps cannot stack multiple queued attacks. Expired recovery resets the chain to its opener. Equipment changes, suspension, character changes and disable still cancel combat.

No global root motion, attack teleports or gameplay-side contact delays were introduced. Selected imports bake root rotation into the pose so the body turn survives retargeting while `applyRootMotion` remains false. Runtime code contains no Motifect file names. Trimmed clips are ordinary Humanoid `.anim` assets with offline-resampled curves, closed-fist muscle values and authored events. The original FBXs remain available.

## Complete pack disposition

All 40 FBXs were imported as Humanoid and sampled on Amy. Selected candidates received denser pose/root/contact review and SportyGranny checks. “Parked” does not mean a file was removed. Non-selected import settings are audition settings, not a claim of release-ready configuration.

**Used now:** `jab_left`, `cross_right`, `combo_hook_uppercut`, `front_kick`, `roundhouse_kick_right` — roles in the table above.

**Parked for later (24):**

| Clips | Reason / possible use |
|---|---|
| `jab_right`, `cross_left` | Alternate lead-hand combinations; avoid adding variety without a coherent chain. |
| `hook_left`, `hook_right` | Standalone hooks have a narrower stance; would benefit from authored foot/hip matching before reuse in this chain. |
| `uppercut_left`, `uppercut_right` | Alternative finishers; larger lean/pose changes than the selected linked follow-up. |
| `body_punch_right` | Future low/body attack when target-height behavior is deliberately supported. |
| `knee_strike`, `elbow_strike_right` | Close-range technique candidates; not forced into the basic chain. |
| `roundhouse_kick_left`, `side_kick_right` | Alternate/special kick candidates; wider silhouette and different recovery. |
| `block_high`, `block_mid`, `block_low`, `parry_right` | Future defensive actions; no new block/parry mechanic added. |
| `dodge_left`, `dodge_right`, `dodge_back` | Future deliberate dodge displacement; existing movement was not repurposed. |
| `hit_react_body`, `hit_react_face`, `hit_react_chest`, `stagger_backward` | Body/face reaction and stagger candidates. Body reaction is the best compact starting candidate. See reaction limitation below. |
| `combo_jab_cross` | Useful baked combination, but its larger stepping/lunge behavior is less compatible with the current in-place opener. |
| `combo_punch_kick` | Future authored mixed combination; basic V1 currently keeps each contact under its own deliberate input. |

**Not currently suitable for basic production V1 (11):**

| Clips | Reason |
|---|---|
| `knockdown_fall`, `knocked_to_knees`, `get_up` | Need deliberate root-height/support/contact treatment and an actual knockdown/recovery contract. Raw in-place audition does not establish grounded fall/get-up correctness. |
| `jump_kick`, `spinning_kick`, `back_kick`, `sweep_kick`, `stomp` | Special/acrobatic or grounded-target actions, not a coherent default boxing-style chain. Preserved for future techniques. |
| `grab_and_throw`, `choke_grab` | Need paired target animation, contact and interaction semantics. A lone actor clip is insufficient. |
| `roll_dodge_forward` | Needs collision-aware deliberate displacement and recovery; not added through animation root motion. |

## Reactions and manual-editing limits

Reaction, knockdown and get-up sources were auditioned, but **new player reaction/knockdown gameplay is not implemented**. The current player has no existing `IHitReaction` animation consumer or knockdown state. The existing target damage/reaction pipeline is preserved. Wiring a falling pose onto unrestricted movement would not satisfy the quality requirement.

The best follow-up for an animator is to refine finger/thumb contact and shoulders for Amy, then check SportyGranny's thicker arms. Standalone hooks need stance matching before joining this chain. Knockdown/get-up needs paired ground contact and root-height editing. Those assets should not be labelled production-ready on the strength of a successful Humanoid import.

Amy and SportyGranny use the same controller, mappings and semantic actions. No runtime character-specific animation code was introduced. Different proportions still change fist/torso clearance and the strength of the silhouette.

## Review tools and validation

The historical animation audition window has been retired. Review the authored clips and `HumanoidShooter.controller` in Unity, then exercise the complete chain in normal Play Mode on Amy and SportyGranny. `FightingPlayModeTests` retains the real-controller/input sequence regression.

The selected derivatives, typed impact events and animation mappings are already authored; the one-time generator is retired. Edit those assets for future animation work, using the timing table above as the historical recipe. Canonical player setup still supplies melee, animation and primary-action wiring.

Validation artifacts:

Final combined Unity run: **51 passed, 0 failed**. The native Play Mode fixture completed eight scenarios: six default-chain runs across Amy/SportyGranny and two moving alternate-kick runs on Amy. The controller review separately completed three cycles for each of six movement scenarios on both characters (36 cycles).

- `Logs/CombatV2-tests.xml`: focused Unity tests covering melee input/queue/physics/events/cancellation, authored content, guard foot placement, shared-character setup, grenade animation regressions and the native Play Mode sequence fixture.
- `Logs/CombatV2/play-review.txt`: real ConstructionSite player/input/controller checks on a disposable clear test lane. Test persistence is isolated in a disposable test directory.
- `Logs/CombatV2/Sequences/`: real-controller sequences, sampled every .1 s after 60 Hz stepping. Each standing/directional scenario runs three uninterrupted cycles on both Amy and SportyGranny.
- `Logs/CombatV2/diagnostics.txt`: retargeted source hand and foot samples used to locate contact.
- `Logs/ProceduralUI/combat-sequence-*.png`: Play Mode captures through the existing gameplay camera.

The captures were visually inspected; automated checks establish sequencing, state recovery, movement and event behavior. They do **not** certify the subjective “production-worthy / seamless at normal speed” acceptance criterion. Final live art acceptance requires normal Play Mode review. No Android/device test was performed. A pre-existing MobileAimSettings warning about chances totalling 85% remains unrelated to this work.

Final acceptance checklist: watch the three requested full sequences at 1×; check the hook/finisher junction, kick support foot, idle/guard foot placement and moving upper-body silhouette; repeat with target/facing changes in the actual level. Reaction/knockdown acceptance remains open for the reasons above.

## Changed files

- `Assets/Game/Animations/Combat_v2/`: 40 retained source FBXs with Unity import metadata; seven selected derivative clips and `Fighting/Footwork.mask`.
- `Assets/Game/Animations/Player/HumanoidShooter.controller`, `HumanoidAnimationActions.asset`, `AM_UnarmedCombat.mask`: selected states, semantic timing and layers.
- `Assets/Game/Data/Items/UnarmedCombat/Fighting.asset`, `Assets/Game/Scripts/Items/UnarmedCombatItemData.cs`: authored five-step chain and planted-attack list.
- `Assets/Game/Scripts/Player/CharacterActionId.cs`, `CharacterAnimationActions.cs`, `CharacterAnimatorDriver.cs`, `PlayerAnimation.cs`, `PlayerMeleeController.cs`: semantic actions, recovery gate, presentation blending and one-step queue.
- `Assets/StarterAssets/ThirdPersonController/Scripts/ThirdPersonController.cs`: narrow planted-kick speed condition.
- `Assets/Game/Editor/FightingAnimationAuthoring.cs`, `FightingAnimationReview.cs`, `CombatV2Audition.cs`, `MeleeMotionReview.cs`: reproducible authoring, isolated review and captures.
- `Assets/Game/Tests/Editor/Core/PlayerMeleeTests.cs`, `UnarmedCombatContentTests.cs`, `FightingPlayModeTests.cs`: affected behavior and content validation.

Related Unity `.meta` files are retained/generated normally. No gameplay scene, weapon, enemy, UI, Knowledge, camera, Beam, inventory or persistence implementation was changed.

