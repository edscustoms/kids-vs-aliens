# Alien Ground Aurora V1 — authored tuning

ConstructionSite owns one explicit `PF_AlienGroundAurora` instance. This is optional
level presentation, not a required gameplay dependency or an Active Run participant.
It does not change collision, visibility queries, health, input, audio or Beam transport.

## Ownership and assets

- `Assets/Game/Scripts/World/AlienGroundAuroraController.cs`: one scaled clock,
  chronological schedule cursor and fixed pool playback.
- `AlienGroundAuroraPatch.cs` beside it: authored transform and shader properties only.
- `AlienGroundAuroraSchedule.cs`: accepted serialized event records, read-only at runtime.
- `AlienGroundAuroraAuthoring.cs`: inputs on the scene's `Authoring (Editor only)` child,
  tagged `EditorOnly` so scene references/authoring data are removed from builds.
- `Assets/Game/Editor/AlienGroundAuroraBaker.cs`: explicit Inspector bake and validation.
- `Assets/Game/Prefabs/Environment/PF_AlienGroundAurora.prefab`: controller and 14 pooled
  children, using `PF_AlienGroundAuroraPatch.prefab`.
- `Assets/Game/Art/Environment/AlienGroundAurora/ConstructionSite_Aurora.asset`: scene bake.
  The same folder owns the shared ribbon mesh, flow texture and material.
- `Assets/Game/Shaders/VFX/AlienGroundAurora.shader`: unlit additive energy, using the
  same cyan/violet visual family as Beam without referencing its gameplay or modifying it.

## Explicit authoring workflow

Select `PF_AlienGroundAurora/Authoring (Editor only)` in ConstructionSite. Inspect its
allowed zones, explicit ground colliders, exclusions and schedule settings. Change the
seed for another authored variation, then press **Bake / Regenerate Schedule**.
The schedule asset lists all event positions, rotations, dimensions, timings, fade
durations, colors, intensity, motion speed/phase, footprint parameters and pool slots. Selected authoring
gizmos show zones and baked footprints. There is no automatic bake on reload, save,
Play or scene repair. A failed bake leaves accepted schedule data intact. After changing
level geometry, explicitly rebake and review clearance before accepting the new layout.

**Event Count** is the exact number of distinct spots/events in a loop: 4 creates 4,
19 creates 19. It is independent of **Maximum Active** (simultaneous events) and the
14-object pool. **Candidate Location Count** is only the Editor search budget; unused
candidates do not become events. **Schedule Duration** spreads those events over time;
**Timing Spread** varies the seeded track start offsets and rests. Count is distributed
across circular tracks including any remainder, never rounded to a pool-size multiple.
Impossible timing/clearance fails explicitly and preserves the accepted asset. Bounded
seeded assignment retries help fit unique spots without relaxing exclusions or spacing.

The baker samples the actual main terrain (80 × 100 m), using an inset 76 × 96 m zone
inside the measured perimeter fence.
Editor raycasts reject nearer roofs/props and slopes above 12 degrees. A 9 × 5 grid
checks the complete conservative ribbon footprint against ground support and planarity;
an overlap box covers the raised wisps as well as thin blockers between samples.
Major renderer bounds also reject overhead/uncollided structures. Explicit exclusions cover LevelStart, the Healing Pod
and both excavators, with clearance around their interaction areas.

Candidate locations have 2 m minimum center spacing: broad fields at different times
may occupy nearby ground. Concurrent events additionally
separate their full footprints, including across the loop seam. Each baked pool slot
has a circular track with a positive rest between events; it cannot truncate another
event's lifetime. The bake is seeded in the Editor and deterministic. Runtime has no
random-number generator, ground search, physics placement query or environment scan.

## Accepted schedule and rendering budget

Seed 73129 selects **19 distinct events over 96 seconds** from 83 validated candidates
(search budget 128). The authored simultaneous cap is **4**, with **1–4 active** and a
mean of **2.86**. Broad, longer-lived fields replace the prior many short events. The
pool still contains 14 objects; raising the cap requires sufficient valid space/timing.

| Inspector setting | Current scene value |
| --- | --- |
| Lifetime minimum / maximum | 8 / 20 seconds |
| Length minimum / maximum | 9 / 18 m |
| Width minimum / maximum | 4.5 / 8 m |
| Fade-in minimum / maximum | 2.5 / 3.5 seconds |
| Fade-out minimum / maximum | 3 / 4 seconds |
| Edge Softness / Irregularity / Shape Variation | 0.4 / 0.85 / 1 |
| Timing Spread | 0.75 |
| Location Spacing / Active Spacing | 2 m / 0.8 m |
| Motion Speed (unchanged) | 0.30–0.45 |

The accepted bake spans 9.24–15.75 m long and 4.51–7.91 m wide, with an average bounding
ground area of **63.3 m²**, up from **20.9 m²** (3.03×). This measures the baked envelope;
the visible organic mask occupies less than the full rectangle. Height stays unchanged.

Each patch is one mesh/renderer containing three broad, arched haze layers: 891 vertices /
1,536 triangles, one submesh, the existing shared material and baked 128 × 128 flow texture.
The sheets start near ground level and roll upward, with different offsets and curves.
The accepted mesh, material, palette, vertical shape and motion equations are unchanged.
Two scrolling texture samples still bend directional wisps through the softer cloud body.
Shader displacement reaches 0.12 m vertically, 0.065 × patch width laterally and
0.035 × patch length longitudinally; total crest height stays around 0.7–0.8 m.
Cyan/teal and violet accents follow the patch's authored phase, with hue aligned across
layers to limit pale additive overlap. Edge erosion and smooth zero-opacity boundaries
hide the rectangular sheet borders. Motion consumes authored event age, never wall time.
The new static perimeter mask merges uneven lobes/branches and erodes their edges using
one additional sample of the same noise texture. Softness, irregularity and shape offsets
are stored in each baked event and sent through the existing property block at activation.
Shape Variation controls the range of those seeded offsets; zero repeats the same mask.
The mask stays within the validated footprint and fades on all sides. No runtime shape
selection, mesh generation or placement work occurs.

Longer smoothstep fades use the existing envelope; the remaining lifespan is the hold.
Both fades are included in the 8–20-second lifespan; the baker requires a visible hold.
No extra activation, randomization or per-frame CPU mesh work was added.

Geometry remains **1,536 triangles per patch**. This bake peaks at **4 draws / 6,144
triangles** before culling; the unchanged 14-slot pool can support 21,504 triangles.
Larger footprints and overlapping sheets increase transparent pixel coverage
and overdraw, particularly at grazing angles. This tradeoff needs device profiling;
triangle/draw budgets do not establish GPU cost. There are no additional renderers,
particles, lights, shadows, colliders or material copies. Controller, pool and playback
state machine are unchanged. The one-off tuning helper was removed after authoring.

## Runtime lifecycle

The pool already exists in the prefab: even initialization creates no GameObjects.
Awake allocates two small slot arrays and one MaterialPropertyBlock per patch.
Steady playback visits 14 slots plus newly due events (about 0.2 starts/second), applies
serialized transforms at activation and updates only active shader properties.
The warmed playback test measures 0 managed bytes allocated across 1,600 updates;
there is no runtime collider/mesh reconstruction.

Events crossing the loop boundary continue at their proper age; there is no mass reset.
Startup includes the preceding cycle's tails. A long time step seeks directly into the
same baked cycle. Disable hides the pool; reenable starts the same deterministic cycle.
Scaled time and explicit shader age preserve world pause. These transient decorative
frames are not saved; Continue starts the schedule normally without altering run state.

## Validation

`AlienGroundAuroraTests` covers serialized circular timing/spacing, actual scene ground
and landmark clearance, shared rendering settings, explicit deterministic rebaking and
failed-bake preservation, lifetime fades, pool identity, pause/loop/hitch/reenable behavior,
steady-state managed allocations and real URP rendering/motion captures.

Unity 6000.5.6f1: `AlienGroundAuroraTuning-Final` passed **20/20** (8 Aurora, 7 Healing
Pod route/collision, 5 LevelStart checks). This includes exact distinct counts of 4 and
19, seeded repeatability, early capacity rejection without changing the accepted asset,
baked footprint parameters reaching the existing property block, full scene clearance,
zero managed allocation across 1,600 warmed playback updates, and real URP rendering.
Fades have first/last-quarter-second opacity below 10% and bounded materialization steps.
No Full regression was run because shared gameplay systems were unchanged.

Gameplay captures use the normal lens/player-relative offset with Amy beside a patch.
The reference event is chosen with enough full-opacity time remaining to show actual
flow, not just a fading image. Captures also include two low three-quarter ages, the
wider site, and 41 frames across a real authored 14.89-second event. `playback.html`
plays/scrubs that sampled lifecycle; other events are frozen in
that sequence to isolate this event's materialize/flow/dissolve behavior.

XML/logs are in `Logs/RepositoryAuditRemediation`; overview, close-up, motion and
gameplay PNGs and the playback viewer are retained in `Logs/AlienGroundAuroraTuning`.
Relative to the previous pass, ConstructionSite changes only the Aurora authoring record.
The Healing Pod prefab, all other scene records, the Aurora mesh/material/controller,
the palette and shader color/motion equations are unchanged. Earlier denser bake attempts
were rejected by the existing clearance rules; the final authored cap/spacing fits all
19 distinct fields without changing those exclusions.
`git diff --check` passed.

Manual checks remain: look while walking the whole route, camera-visible density,
brightness against enemies/items, and Android/iOS GPU overdraw, frame rate and thermals.
Editor tests and estimated geometry cost do not establish device performance.
