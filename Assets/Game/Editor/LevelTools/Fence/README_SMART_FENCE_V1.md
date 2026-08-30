# KVA Smart Fence Linking V1

This builds on the working configurable fence + collider system.

## Workflow

1. Drag `PF_FenceSection` into the scene.
2. Configure height, spacing, pole style/materials as usual.
3. Select that scene fence.
4. In the Inspector under **Smart Fence Linking** choose:
   - How Many
   - Extend from LEFT or RIGHT pole
   - Straight / Up Turn / Down Turn
5. The first extension automatically converts the standalone fence to a `FenceRun`.
6. Continue by selecting any generated `Segment_*` object and extending again.

## Shared poles

The run owns unique pole nodes.

- 1 section = 2 poles
- 2 connected sections = 3 poles
- 3 connected sections = 4 poles

When a new endpoint lands on an existing pole position, that existing pole is reused.
Duplicate segments are also prevented.

## Generated hierarchy

```text
PF_FenceSection_Run
├── Poles
│   ├── Pole_000
│   ├── Pole_001
│   └── Pole_002
└── Segments
    ├── Segment_000_001
    │   ├── Rail_Top
    │   ├── Rail_Bottom
    │   ├── ChainLink
    │   └── Collision
    └── Segment_001_002
        ├── Rail_Top
        ├── Rail_Bottom
        ├── ChainLink
        └── Collision
```

Each section has one cheap BoxCollider. Shared poles are visual nodes, so no duplicated
pole colliders are needed.

## Direction behavior

For the initial horizontal prefab:

- LEFT Straight goes left
- RIGHT Straight goes right
- Up / Down create 90-degree turns

For rotated fence segments, the arrows are relative to that selected segment.

## V1 scope

Included:
- straight extension
- 90-degree turns
- count
- shared pole reuse
- loop/T-junction pole reuse when positions match
- same height/material/style/collision as source fence

Not included yet:
- gates/doors
- delete/heal topology controls
- curved fences
- automatic chain-link texture tiling
