# 📱 Performance & Testing

← [Back to README](../README.md)

## Performance philosophy

> **Profile first. Optimize second.**

Unity already handles basic renderer frustum culling. Off-screen objects may still consume CPU, physics, animation, AI, memory, etc.

Do not build custom streaming/culling systems until a real level proves they are needed.

---

## Mobile targets

Current baseline test device:

**OnePlus Nord 3**

Direction:

```text
~30 FPS minimum on lowest supported hardware
~60 FPS on reasonable/mid devices
```

Later test weaker phones.

---

## What matters more than triangle count

A ~10k triangle enemy is a completely reasonable starting point.

Watch:
- SkinnedMeshRenderer count
- number of materials / draw calls
- bones
- transparency
- shaders
- shadows
- animation
- AI
- physics queries
- VFX
- texture memory

Profile the complete encounter rather than optimizing meshes in isolation.

---

## Hot-loop rules

Prefer:
- cached arrays/buffers
- `NonAlloc` physics calls where useful
- pooled high-frequency VFX
- avoiding recurring managed allocations in Update loops

Already refactored examples:
- PlasmaArc index buffer reuse
- enemy `OverlapSphereNonAlloc`
- plasma VFX pooling

---

## Build / asset behavior

Keep test scenes in the project if useful.

Only include required scenes in the player build.

Be intentional with:
- `Resources`
- `StreamingAssets`
- Addressables / AssetBundles

Do not use `Resources` as a generic asset folder.

---

## Quick Editor smoke test

### Menu
- [ ] Browse character
- [ ] Browse weapon
- [ ] SELECT
- [ ] SELECTED
- [ ] PREVIEW
- [ ] NONE
- [ ] BACK
- [ ] PLAY uses selected loadout

### Player
- [ ] Move
- [ ] Sprint
- [ ] Jump
- [ ] Camera
- [ ] Free-look
- [ ] Character rotation
- [ ] Weapon attached correctly

### Combat
- [ ] Shoot
- [ ] Muzzle VFX
- [ ] Bolt VFX
- [ ] Impact VFX
- [ ] Enemy takes damage
- [ ] Practice piece breaks
- [ ] Near-wall shot blocked
- [ ] Low-cover shot still valid

### Aim
- [ ] Acquire target
- [ ] Sticky lock
- [ ] Switch target
- [ ] Camera visibility respected
- [ ] LOS respected
- [ ] No-lock shooting works

---

## Android smoke test

Before important milestones:

- [ ] Launch
- [ ] Loading
- [ ] Menu
- [ ] Loadout
- [ ] Scene transition
- [ ] Landscape
- [ ] Multitouch
- [ ] Move / look
- [ ] Jump / sprint
- [ ] Aim / auto-aim
- [ ] Shooting
- [ ] Enemy damage
- [ ] Player damage
- [ ] VFX
- [ ] Wall fade
- [ ] Scene restart
- [ ] Performance feels stable
- [ ] Android Logcat clean of serious errors

---

## Automated tests

Keep automated testing **small and high-value**.

Best candidates:
- loadout state
- combat hit resolver
- menu selection state
- VFX pool reuse
- future save/progression
- future objective state logic

Do not try to unit-test “camera feels good” or “movement is fun”.
