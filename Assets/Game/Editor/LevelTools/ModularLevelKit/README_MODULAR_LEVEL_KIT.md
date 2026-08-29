# Kids VS Aliens — Modular Level Kit V3

V3 changes the wall strategy after testing V2 in Unity.

## Wall decision: sharp mitered turns

The previous overlap / octagon-hub junctions were the wrong abstraction for normal level boundaries.

Production walls now use **mitered corners** (Dutch: *verstekhoek* / a sharp cut where the wall faces meet exactly).

A bent wall strip already contains both:
- the inner/concave edge;
- the outer/convex edge.

Therefore normal turns only need direction variants:

```text
Corner_90_Left_1m
Corner_90_Right_1m
Corner_45_Left_1m
Corner_45_Right_1m
```

No separate "inside corner" and "outside corner" prefab is required for ordinary turns.

If Level 1 later genuinely needs a T/Y junction, add that as a dedicated module based on the actual layout instead of maintaining eight speculative junction pieces.

## Fixed in V3

- 90° corners are exact sharp mitered geometry; no cube overlap/notch.
- 45° corners are exact sharp mitered geometry; no octagonal center hub.
- Triangular floors get a generated URP/Lit grey fallback material when no floor material is selected.
- Ramp gets the same fallback, preventing the pink/missing-shader look.
- Existing stairs remain unchanged.
- Straight walls remain length-stretchable on local X.

## Generate

Unity:

`Tools > Kids VS Aliens > Level Tools > Generate Starter Modular Kit`

Recommended:

```text
Wall Height      2.0
Wall Thickness   0.25
Elevation Height 0.5
Corner Arm Length 1.0
```

If Wall/Floor Material is empty, the tool creates and uses:

`M_GreyboxFallback`

under the generated prefab folder.

## Starter pieces

```text
Wall_1m
Wall_2m
Wall_4m
HalfWall_2m

Corner_90_Left_1m
Corner_90_Right_1m
Corner_45_Left_1m
Corner_45_Right_1m

Floor_2x2
Floor_3x3
Floor_4x4
FloorTri_2x2_45
FloorTri_3x3_45
FloorTri_4x4_45

Platform_2x2_H0.5
Stairs_H0.5
Ramp_H0.5
```

## Legacy V2 pieces

After removing any test instances from the scene, the old generated pieces can be deleted manually:

```text
Corner_90_1m
Junction45_Left_*
Junction45_Right_*
```

They are intentionally not deleted automatically so the editor tool never destroys scene references behind your back.

## Snapping

The snapping workflow is unchanged:

`Tools > Kids VS Aliens > Level Tools > Modular Snap`

Fast mode:
1. select two module roots;
2. make the module that should MOVE the active/last selection;
3. `Snap Closest Compatible Sockets`.

Exact mode still works with two selected `Snap_*` children.

## Scaling straight walls

Long straight run:

```text
Wall_2m
local Scale X = 4
=> 8m long wall
```

Good:
- stretch local X for length;
- keep Y/Z standardized.

Do not non-uniformly stretch corner, stair or ramp modules.

## Build impact

All generator/snapping scripts stay under `Assets/Game/Editor/...` and are Editor-only. They are not compiled into Android/iOS/player builds.


## V4 visual fix

V3 custom-generated meshes shared vertices between neighboring faces and then called
`RecalculateNormals()`. Unity therefore averaged normals across hard construction edges,
which made ramps, triangle floors and mitered corners look rounded/hollow/see-through.

V4 splits vertices per face before recalculating normals. The geometry is still the same,
but every construction edge now shades flat and solid.

After installing V4, run **Generate / Regenerate Starter Kit** once to update the mesh assets
and prefabs already used by the level.


## V5 — corner fix + intuitive mixed-size floor snapping

### 45° / 90° wall corners

V5 rebuilds the corner pieces as **one continuous mitered wall mesh**.

The previous version split the turn into two generated mesh pieces. The new version uses:
- one continuous thick-wall footprint;
- true sharp inside/outside miter points;
- concave-polygon triangulation;
- corrected Unity X/Z face winding;
- one visual + one MeshCollider.

Regenerate and test:

```text
Wall_2m
→ Corner_90_Left_1m
→ Wall_2m
```

and:

```text
Wall_2m
→ Corner_45_Right_1m
→ Wall_2m
```

### Snapping a 2x2 floor onto one half of a 4x4 floor

No special setup is required.

A generated rectangular floor now has invisible logical snap choices along its edges at useful 0.5 m offsets.

Workflow:

```text
1. Put the 4x4 target in place.
2. Drag/duplicate a 2x2 floor.
3. Roughly move the 2x2 near the LEFT or RIGHT half you want.
4. Select the 4x4.
5. Ctrl-select the 2x2 LAST so the 2x2 is active.
6. Click "Smart Snap Closest".
```

Fast Snap always uses the moving piece's **center edge socket**, while the target can use its extra edge slots.

So a 4 m edge supports useful target centers at:

```text
-1.0   -0.5   0   +0.5   +1.0 m
```

This also makes 3x3 ↔ 2x2 and 4x4 ↔ 3x3 placement easy.

If Smart Snap ever chooses a slot you do not want, Exact Snap still works with any `Snap_*` child.


## V6 — cleaner Scene view + explicit center-on-side snapping

### Scene view is no longer a text explosion

Socket names are **OFF by default**.

Visual language:

```text
primary socket  = cyan dot + small arrow
offset/half slot = tiny cyan dot
text labels      = optional toggle
```

Open:

`Tools > Kids VS Aliens > Level Tools > Modular Snap`

Enable **Show socket names** only when debugging exact sockets.

### Snap a smaller piece exactly to the middle of a bigger side

Use the new:

`Snap Edge Centers`

Workflow:

```text
1. Select the big/target module.
2. Ctrl-select the small/moving module LAST.
3. Put the small module roughly near the side you want.
4. Click "Snap Edge Centers".
```

This deliberately ignores all generated half/offset slots and connects the nearest
**primary edge midpoint** to the nearest compatible **primary edge midpoint**.

Example:

```text
        2x2
       +---+
       |   |
+------+---+------+
|                 |
|       4x4       |
|                 |
+-----------------+
```

Use:
- **Smart Snap Closest** when you want left/right/offset placement.
- **Snap Edge Centers** when you want exact middle-of-side placement.
- **Exact Snap** only for unusual/manual cases.


## V7 — whole-group snapping + 1x1 floor family

### Group Snap

The tool can now snap an entire assembled chunk to another assembled chunk.

Example hierarchy:

```text
WakeArea
├── Floor_4x4
├── Floor_4x4
├── Wall_4m
└── ...

PathChunk_01
├── Floor_2x2
├── FloorTri_2x2_45
├── Wall_2m
└── ...
```

Workflow:

```text
1. Roughly place PathChunk_01 on the side of WakeArea where it should connect.
2. Select WakeArea first.
3. Ctrl-select PathChunk_01 LAST.
4. Click "Snap Group To Side Center".
```

The tool calculates the combined visible bounds of both groups.

It then:
- detects which side the moving group is roughly sitting on;
- makes the two outer edges touch;
- centers the moving group on that side;
- moves the WHOLE parent as one unit;
- preserves the group's internal layout;
- preserves elevation/Y.

No group sockets or extra setup are required.

This first version intentionally targets cardinal outer sides (left/right/top/bottom).
Angled group-boundary snapping can be added later if actual level design needs it.

### Cleaner Scene view

Socket drawing is now OFF by default.

If enabled:
- only sockets directly under the selected individual module are drawn;
- selecting a whole room/group does NOT draw every descendant socket;
- offset slots are hidden unless explicitly enabled;
- socket names are hidden unless explicitly enabled.

### New generated floors

```text
Floor_1x1
FloorTri_1x1_45
```

The existing families remain:

```text
Floor_2x2
Floor_3x3
Floor_4x4

FloorTri_2x2_45
FloorTri_3x3_45
FloorTri_4x4_45
```

Because 1x1 is now the smallest floor module, generated rectangular edge-slot positions
also support correct 1 m placement.


## V8 — correct group corner snapping

The V7 group command centered the **whole moving group** against the whole target side.
That was not the intended level-design workflow.

V8's main group command is now:

`Snap Group Corner → Side Center`

Example:

```text
TARGET GROUP (1)

+-------------------+
|                   |
|                   |  ← side center X
|                   |
+-------------------+


                         MOVING GROUP (2)
                         +---------+
                         |         |
                  corner X         |
                         +---------+
```

Workflow:

```text
1. Roughly place group 2 beside the side of group 1 you want.
2. Select group 1 first.
3. Ctrl-select group 2 LAST.
4. Click "Snap Group Corner → Side Center".
```

The tool:
- chooses the target side from the rough placement;
- uses the center of that target side;
- finds the nearest facing corner of the moving group's bounds;
- moves the whole group so that corner lands exactly on the side center;
- preserves Y/elevation and the internal layout.

The previous behavior is still available as:

`Snap Group Center → Side Center`

so both workflows are available.


## V9 — actual exposed-boundary snapping

The previous group-snap versions used rectangular group bounds. That does not work for
real level chunks with corridors, notches, triangles and irregular outlines.

V9 replaces that workflow with **actual exposed floor boundary snapping**.

### Main workflow

#### 1. Store the target

Select either:

```text
WakeArea
```

or several loose target pieces:

```text
Floor_A
Floor_B
Floor_C
```

Then click:

`Set Target From Selection`

#### 2. Select what should move

This can also be:

- one floor prefab;
- one parent containing a whole assembled room/path;
- several loose connected prefabs selected together.

Roughly place the moving selection near the target edge where it should connect.

Then click:

`Snap Moving Selection To Target`

### What the tool calculates

It does NOT use a rectangular group bounding box.

For generated square/triangle floor pieces it:

1. reads the real horizontal top geometry;
2. finds the real outer/exposed boundary;
3. removes internal edges between connected floor modules;
4. merges connected collinear boundary pieces into real outer edge runs;
5. finds the nearest sensible pair of facing, parallel exposed edges;
6. snaps:

```text
moving exposed-edge midpoint
            ↓
target exposed-edge midpoint
```

The entire moving selection translates as one rigid assembly.

### This covers both requested cases

Parented chunk:

```text
TARGET ROOM              MOVING ROOM
+---------+                    +------+
|         |                    |      |
|       +-+      ← snap →   +--+      |
|       |                 corridor    |
+-------+                    +---------+
```

Loose selection:

```text
Select Floor_1 + Floor_2 + Floor_3
→ they act as one temporary moving assembly
→ no temporary parent is required
```

### 1x1 floor fix

For floor layout, use the new Boundary / Chunk Snap instead of the legacy socket button.

Because it operates from the real floor geometry, `Floor_1x1` and
`FloorTri_1x1_45` are handled exactly the same way as larger floor pieces.

Socket snapping remains available for:
- walls;
- stairs;
- ramps;
- exact prefab connection work.

### Elevation

Boundary snapping currently moves only X/Z.

It deliberately preserves all Y positions in the moving selection, so snapping a chunk
does not silently destroy an elevation layout. Vertical/elevation-aware connection rules
can be added later when real level design proves which behavior is needed.


## V9.1 — Unity 6.5 compatibility fix

Unity 6.5 deprecates `Object.GetInstanceID()`.

V9.1 removes all `GetInstanceID()` usage from the modular snapping tools:
- visited modules are tracked directly as `Transform` references;
- stored target selections are serialized as `GameObject` references;
- selection overlap checks use direct object references.

No generated level prefabs need to be regenerated for this compatibility-only update.


## V9.2 — compile fix

Fixed `CS0844` in `ModularSnapWindow` caused by a local variable shadowing
the serialized `targetObjects` field.

No prefab regeneration is required.
