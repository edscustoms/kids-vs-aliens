# 🧪 Core Regression Tests

These tests intentionally protect **only reusable core contracts**.

They are not a goal by themselves. If a test does not protect an important framework behavior, do not add it.

## Run

In Unity:

```text
Window
→ General
→ Test Runner
→ EditMode
→ Run All
```

All current automated tests are tagged:

```text
Core
```

So the suite can also be filtered by the `Core` category.

## Current flows

### 1. Loadout state
Protects:
- selected character + weapon initialization
- explicit `NONE` weapon behavior
- clearing the loadout so direct-scene defaults can work again

### 2. Combat hit resolution
Protects:
- child collider → parent `IDamageable`
- damage delivery
- delayed `IHitReaction` contract
- unsupported colliders doing nothing safely

### 3. VFX pooling
Protects:
- release → reuse
- separate prefabs stay in separate pools

## What we deliberately DO NOT automate

Do not add automated tests for subjective game feel:

- movement feels good
- camera feels good
- aim assist feels right
- animations look right
- enemy difficulty
- grenade feel
- level design

Those belong in manual gameplay / device testing.

## Maintenance rule

Add or change a test only when:

1. a tested core contract intentionally changes,
2. a new reusable framework contract is introduced, or
3. a real regression happens and is important enough that we never want it again.

Do **not** create a test just because a new class exists.

## Why these tests live under `Editor`

The project currently compiles normal gameplay scripts into Unity's predefined `Assembly-CSharp` assembly.

Keeping these tests inside an `Editor` folder lets them:
- access the existing gameplay code without restructuring the whole project into asmdefs,
- use NUnit through Unity's Editor test environment,
- stay out of normal player builds.

Do not add a test `.asmdef` here unless the runtime project is intentionally migrated to assembly definitions too.
