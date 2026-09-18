Beam Hoist fixed start-zone presentation

Authoring
- Add BeamHoistSurface to an upright platform/container in a gameplay scene.
- The scene's player BeamHoistAbility is the single source for maximum lateral reach,
  minimum vertical gain, maximum landing height and the existing path settings.
- There is no independently authored Approach Width. The baker subtracts landing
  inset and accounts for tangential reach, then checks supported starts and routes
  using the same ability/transport queries as activation (without Knowledge gating).
- Baking conservatively samples 0.25 m depth bands at corners, edge midpoints and
  center. Only accepted cells are stored. This is a finite sampled bake; the existing
  authoritative collision check on Jump and throughout transport still applies.
- Adding/selecting a surface, changing ability settings, saving a scene, entering
  Play Mode, building, and Setup or Repair Active Gameplay Scene refresh the bake.
  Refresh Cached Hoist Geometry explicitly refreshes after surrounding geometry edits.
- A surface prefab without a scene player cannot bake scene-specific constraints;
  place it in a configured gameplay scene. Its bake status explains missing wiring.
- Runtime-added surfaces must carry their scene-authored bake. No runtime baker runs.

Fixed footprint and state
- Gameplay and presentation read the same BeamHoistSurface approaches.
- The presenter snapshots all cells on registration and builds the complete mesh
  once on first discovery. Distance, Amy's position, input/Knowledge state, ability
  limit changes and route availability never remove cells or rebuild that mesh.
- Ground alignment/tessellation occurs once, preserves the baked X/Z perimeter,
  and shares the existing glyph UV layout across adjoining ground cells.
- Disconnected valid regions stay disconnected; invalid gaps are never filled.
- A scene/save rebake changes authoring data for the next runtime instance, not the
  currently revealed mesh. Moving platforms are outside the static surface workflow.
- Knowledge/input/transport unavailability hides immediately. Normal distance exit
  fades out with hysteresis. Re-entry reuses the same instance and mesh.
- The existing valid-start query controls idle versus active power-up state only.
  A dynamic obstruction can remove active state but cannot reshape the pad.

Inspector locations
- Player > BeamHoistAbility > Ability Limits: single gameplay/bake tuning source.
- Player > BeamHoistZonePresentation > Discovery: Reveal Distance (6 m), Hide Distance
  (7 m), Reveal Fade Duration (0.4 s), Hide Fade Duration (0.35 s).
- PF_BeamHoistZoneVFX: existing visual strength and motion settings, unchanged.
- M_BeamHoistZone / M_BeamHoistZoneMotes: existing colors/materials, unchanged.

Files changed for the fixed-footprint correction
- Scripts/Gameplay/BeamHoistZonePresentation.cs: fixed full mesh and state-only updates.
- Scripts/Gameplay/BeamHoistZoneVFX.cs: shared fixed-footprint UVs with ground tessellation;
  Present/flicker/glow/particle behavior unchanged.
- Scripts/Gameplay/BeamHoistAbility.cs: read-only reach, shared existing path constructor,
  editor notification when its settings change.
- Scripts/Gameplay/BeamHoistSurface.cs: remove independent width; expose existing clearance.
- Editor/Helpers/BeamHoistSurfaceBaker.cs: derive and validate stored starts from gameplay.
- Editor/Helpers/BeamHoistZoneSetup.cs: central repair rebakes; ConstructionSite migration.
- Editor/Helpers/BeamHoistZoneReview.cs: actual-scene capture positions use baked data.
- Tests/Editor/Core/BeamHoistZoneTests.cs: bake/source/state/mesh invariance coverage.
- Scenes/ConstructionSite.unity: rebaked surface, prior player observer, user edits preserved.
- Docs/13-BEAM-HOIST-ZONES.md: these notes.

The earlier initial zone implementation also introduced the two materials, shader,
PF_BeamHoistZoneVFX, central GameplaySceneSetup call, and read-only transport queries.
No transport advancement, Bezier path, input, gravity, CharacterController mechanics,
camera, Knowledge logic or level-start arrival behavior was changed by this correction.

Verification (18 September 2026)
- Runtime and Editor scripts compile (existing unrelated warnings).
- All 9 BeamHoistZoneTests pass, including entire mesh vertices/indices/world transform
  unchanged through walking, fades, re-entry, disable and route availability changes.
- Core: 149/155 pass; six baseline failures remain in older BeamTransport tests
  (old path/beam assumptions and cone-prefab assertion) and the paused camera test.
  Logs/HoistFixed-Final-tests.xml contains the complete results.
- ConstructionSite GPU captures: Logs/HoistZone/nearby.png and active.png.
  Available and Active both retain the same two fixed regions: the main side pad
  and a separate valid pocket around the container end. The extra right strip is gone.
- Shader, both materials and prefab hashes match the approved appearance before correction.
- LevelStart hierarchy/settings preservation and setup idempotence tested on
  ConstructionSite and GamePoc. ConstructionSite rebaked/saved; GamePoc not saved.

Manual/device checklist
1. Unlock Knowledge; approach from beyond 7 m: same pad fades in inside 6 m.
2. Walk around every edge: pad stays fixed; valid start changes only power-up state.
3. Walk away and return: identical outline, position and ground alignment.
4. Jump inside the valid area: pad hides; existing hoist trajectory/timing unchanged.
5. Add a new surface, or change player reach/height and save: bake follows those limits.
6. Check level-start arrival, ordinary Jump, occlusion and fill cost on Android/iOS.
Device testing remains outstanding.
