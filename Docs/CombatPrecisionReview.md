# Combat V2 encounter review

Historical validation results and changed-file lists below describe their original passes. Some one-off helpers have since been retired; see [helper cleanup](LegacyHelperCleanup.md). Current automated validation uses the [Tests README](../Assets/Game/Tests/README.md).

## Baseline (Unity, before runtime changes)

The disposable Play Mode lab uses the scene's real player/input/animation components and `PF_Enemy_Melee_POC_V1`. No scene asset is saved. Its floor/NavMesh is isolated at (1000, 0, 1000), saves use a disposable directory, and off-screen Animators explicitly evaluate their poses. CSV traces and sequential captures are under `Logs/CombatPrecision/baseline`.

Observed controlled cases:

- All five strikes deal 10 damage at 1.45 m root separation. At their impact markers the closest attacking wrist/foot is still approximately 0.74 m (Jab), 0.71 m (Cross), 0.82 m (Hook), 0.73 m (Heavy) and 0.52 m (Kick) from the alien's body collider. This is a real visual miss, not a timing-only problem.
- Player withdrawal, obvious air punches and behind-target cases miss; typed markers and one buffered action already work. Keep those mechanisms.
- Enemy Right Hook deals 10 damage at request time (t=0), with its right wrist about 0.54 m from Amy. The actual hook approaches her at approximately t=0.30–0.33 s.
- All three withdrawal trials and all three 90-degree misalignment trials still take immediate damage. All three interruptions at t>0.06 s also take damage: it was already applied before the visible contact.
- Requests made while the enemy is already in Hit are blocked by the existing movement lock. The bug is not an independent delayed coroutine surviving Hit; the old attack resolves too early and has no contact contract at all.
- One repeated single-alien run stops at 0.854 m, beyond its 0.82 m attack-start range. The approach-ring jitter plus agent stopping distance can leave it unable to attack.
- Amy has no current hurt/stagger animation contract. Nonlethal damage does not interrupt her action. Preserve that policy rather than inventing a new reaction mechanic; cancellation and death must still invalidate contact.

The baseline fixture health is increased for aliens so an entire chain and repeated pressure can be inspected. This is not a production health/balance change. Frame captures are evidence for contact and posture, not a substitute for subjective live playtesting.

## Refinements

1. **Player contact:** the existing semantic action binding now owns a `MeleeContactShape`: left knuckles for Jab/Hook, right knuckles for Cross/Heavy, right toes for kicks. Fist radius is 0.09 m; foot radius is 0.10 m. A marker authorizes one same-frame LateUpdate query after the blended Animator pose is available. The old 0.30 m-radius, 1.35 m-long forward damage capsule is gone. The existing 1.35 m value remains only an additional maximum-distance safety check; it cannot extend a limb's reach.
2. **Player validity:** living/targetable bodies, forward alignment (60 degrees), physical limb overlap and clear geometry are required independently for every strike. Closest actual contact wins; an aim target no longer receives a scoring preference across empty space. No rotation assistance or target teleport was added. Death and cancellation invalidate pending pose queries as well as pending markers.
3. **Enemy contact:** Right Hook has one typed `MeleeImpact` at normalized clip time 0.26 (about 0.286 s of the clip). Requests deal no damage. The existing animation relay identifies the source state; the attack owns one pending contact and checks its current controller/state, target, anatomy, facing and geometry again at contact. Its right-knuckle radius is 0.08 m. Source-attributed contact diagnostics are available through `ContactResolved`.
4. **Interruption:** Hit/Stun movement locks immediately cancel the pending attack and reset its trigger. Death/disable also cancel. Exiting the attack state invalidates stale outgoing events. The enemy holds its existing motor during its committed strike, then releases only its own lock. Hit and Stun retain their ownership. A fresh attack after Hit can damage normally.
5. **Spacing:** the production alien starts attacks at 0.78 m instead of 0.82 m, with 35-degree start alignment and a 60-degree contact limit. The approach ring is 0.65 +/- 0.03 m instead of 0.70 +/- 0.10 m; agent stopping distance is 0.02 m instead of 0.12 m. Navigation avoidance, collision radii and movement speeds are unchanged. Enemy setup creates the required relay and uses matching defaults.
6. **Kick timing:** the Front Kick marker moved from clip time 0.5416667 s to 0.4416667 s. The pose curves, duration, chain gate, planted-foot rule and recovery are unchanged. Baseline contact started around request time 0.433 s; the previous marker arrived around 0.567 s.
7. **Preserved:** Jab/Cross/Hook/Heavy marker times, all combo clips and order, guard/strafe blends, normal-to-Fighting transitions, input callbacks, damage values, health values, player nonlethal hit policy, shared damage resolution and all unrelated gameplay remain unchanged.

## Validation and interpretation

- The completed baseline ran all five player strikes through seven situations, six enemy situations three times each, and three repetitions each of 1/2/3-alien encounters.
- The first refined pass rejected all five former long-range hits, retained all five genuine close hits, and passed enemy withdrawal/alignment/interruption checks and all nine encounters.
- A second expanded pass checked walls, cancel/death/disable/stun, and new attacks after interruption. Every point of observed enemy damage was accounted for by an identified contact inside the fist radius, with no Hit/Stun lock active.
- The existing melee and Fighting animation regression suite passed **29/29**, including eight native scene/input sequences covering Amy, Sporty Granny, strafing/direction changes, the full chain and the alternate kick. Three old fixture expectations were updated to the already-existing permanent Fighting capability model (`CombatEntry`); inventory/Knowledge production code was not changed.
- **Final pass passed:** `Logs/CombatPrecision/final.xml`, with frame traces and images in `Logs/CombatPrecision/refined/`. This includes a 55-case player matrix, 36 enemy trials, a target-switch/death chain, and nine repeated fights. The fixture checks actual limb contact on both sides and accounts for all received enemy damage. The corrected kick marker was observed at request time 0.467 s with toe contact in both close-hit cases.

| Final pressure test | Repetitions | Valid alien contacts per run | Player contacts per run | Finding |
|---|---:|---|---|---|
| 1 alien | 3 | 1 / 1 / 1 | 3 / 3 / 3 | Approaches into usable range; contact is no longer applied at attack request. |
| 2 aliens | 3 | 3 / 3 / 2 | 5 / 3 / 4 | Independent attackers remain valid while another enemy reacts. |
| 3 aliens | 3 | 6 / 4 / 2 | 5 / 3 / 4 | Source accounting passed under overlapping pressure; visual crowding remains. |

Across these fights, 16 valid contacts occurred while a *different* alien was in Hit. None came from the reacting alien's cancelled attack. These are endurance fixtures, not kill-time/balance trials: alien health is deliberately raised and Amy is restored between trials. Target death is tested separately during a live combo.

The encounter tests run at normal game time and use actual input, animation, physics, NavMesh and enemy AI. Visual review uses sequential rendered frames. The tests prove the checked contact/state rules; they do not certify subjective combat feel.

## Remaining limits / production decision

- Body colliders remain the authoritative damage surfaces. This is anatomical limb-to-body-collider contact, not per-triangle skin collision.
- Amy still has no melee hurt animation/stagger contract. A nonlethal hit can land while her own valid strike continues. No new reaction system or stun-lock balance rule was introduced.
- Three-alien pressure remains visually busy; extended arms can overlap even when navigation bodies are separated. The damage sources are now attributable, but crowd readability needs a human gameplay-camera review before final production sign-off.
- No auto-facing redesign, combo lunge, dodge, knockback or magnetic reach was added. A shorter Hook can legitimately miss at a spacing where the Cross or Kick connects.
- Device/low-frame-rate feel and mobile targeting are intentionally unverified in this Unity-only task.
- The death trials also exposed a pre-existing `EnemyDeathSequence` warning about resetting a nonexistent `Hit` trigger. It is unrelated to the new contact contract and was not suppressed or changed.

**Readiness:** suitable for controlled Level 1 encounter iteration with the precision fixes; not a claim that Combat V2 has final production feel. Remaining sign-off is dense-fight readability and manual gameplay feel, followed by the separately planned device review.

## Files and authoring

- Runtime: `PlayerMeleeController.cs`, `CharacterAnimationActions.cs`, new `MeleeContactShape.cs`; `EnemyMeleeAttack.cs`, `EnemyMotor.cs`, `EnemyMovementLockReason.cs`, `EnemyApproachPlanner.cs`. `EnemyBrain.cs` only has an obsolete immediate-damage comment corrected.
- Content: `HumanoidAnimationActions.asset`, `Kick.anim` (marker only), `Right_Hook.fbx.meta` (event only), `PF_Enemy_Melee_POC_V1.prefab` (relay/contact/spacing).
- Authoring: new `MeleePrecisionAuthoring.cs`, `FightingAnimationAuthoring.cs`, `EnemyPocSetupWindow.cs`.
- Tests: new `MeleeEncounterTests.cs`; updated `PlayerMeleeTests.cs` and `FightingPlayModeTests.cs`.
- Tune player bones/radii in **HumanoidAnimationActions > Bindings > Melee Contact**. Tune enemy contact/facing on **EnemyMeleeAttack**, and approach/stop values on the production prefab's **EnemyApproachPlanner / NavMeshAgent**. Markers remain authored animation events. No gameplay clip-name dispatch was introduced.
