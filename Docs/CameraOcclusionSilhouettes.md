# Camera occlusion silhouettes — 20 September 2026

## Scope and cause

Only the line representation changed. CameraOcclusionController's sample generation, collider/group resolution, blocked-sample thresholds, fade state updates, hidden visibility, and material handling are unchanged. CameraOcclusionAuthoring, its MaterialPropertyBlock `_Fade` path, SG_EnvironmentSurface, gameplay cameras, colliders, and production scenes were not edited.

The old controller extracted boundary/sharp edges from readable MeshFilter meshes into line meshes. Unreadable/missing geometry or an empty extracted result fell back to twelve edges of `Renderer.localBounds`. That fallback produced literal boxes. Sharp-edge extraction also retained interior/back-facing structural edges once the object faded. The Shader Graph supplied the surface fade, not these lines.

## Replacement

CameraOcclusionSilhouetteFeature uses the existing controller's currently fading renderers and line-strength calculation. It redraws actual renderer submeshes into a half-resolution RG8 coverage/strength mask with D16 depth, then composites the outer boundary of that coverage. Overlapping child meshes merge into one coverage silhouette; there are no triangle edges or per-child box proxies. Scene depth excludes geometry behind nearer opaque surfaces. Bounds serve frustum culling only.

The contour shader uses a small bilinear neighborhood, the existing cyan/light-blue color and dash settings, and a tunable `Silhouette Width Pixels` field on CameraOcclusionController (default 1.5). The existing line start fade controls appearance/disappearance. No CPU mesh-vertex/triangle traversal is required, including for unreadable imported meshes.

Ordinary MeshRenderers and SkinnedMeshRenderers use the same path. Common URP alpha-cutout materials retain their texture holes. The existing `CameraOcclusionLines=Off` shader tag excludes decorative decals. Mask materials are cached per source material and released on controller changes/disposal; renderer materials/property blocks are not modified by this feature.

## Files and setup

- `Assets/Game/Scripts/World/CameraOcclusionController.cs`: removes generated structural/bounds line meshes; exposes eligible renderers and line settings.
- `Assets/Game/Scripts/World/CameraOcclusionSilhouetteFeature.cs`: URP RenderGraph mask and contour passes.
- `Assets/Game/Shaders/Resources/SH_CameraOcclusionLines.shader`: actual mesh mask and silhouette composite; existing shader asset/GUID reused.
- `Assets/Game/Editor/CameraOcclusionSilhouetteSetup.cs`: idempotent renderer-feature installation.
- `Assets/Game/Editor/GameplaySceneSetup.cs`: invokes that installation from the canonical repair path.
- `Assets/Game/Settings/Rendering/PC_Renderer.asset` and `Mobile_Renderer.asset`: feature installed; preview renderer excluded.
- `Assets/Game/Editor/CameraOcclusionSilhouetteReview.cs`: disposable ConstructionSite Play Mode camera review.
- `Assets/Game/Tests/Editor/Core/CameraOcclusionSilhouetteTests.cs` and `ExcavatorDecalTests.cs`: focused regressions.

New participating ordinary mesh props need no custom outline meshes or per-prop setup. Run the normal gameplay setup/repair to install the shared rendering dependency in older project configurations. Repeated installation preserves feature instances and existing renderer features.

## Unity validation

ConstructionSite Play Mode review used actual gameplay camera follow, normal controller detection/fading, valid disposable player placements, then movement through StarterAssets input. No production scene or loadout was saved. Captures include the same-camera solid reference, faded view, and moved-player view. All captures are under `Logs/OcclusionSilhouettes/`.

- Excavator: outline follows the actual boom/cab/body/track perimeter, with no bounds-box or internal triangle network. Existing close camera framing clips some of the boom. Lines retain the authored restrained opacity/dash style.
- Container: actual visible contour and geometry openings, with other simultaneously faded props included naturally.
- Electrical substation: multi-part silhouette without internal renderer seams; camera framing limits how much of its outer boundary fits onscreen.
- Building/stairs: reviewed several positions, including movement. Existing non-fading rails and structural members still appear. **Head-on stair tread readability remains limited:** railings cover the perimeter, and coplanar/interior tread transitions are not outer silhouettes. This implementation deliberately does not outline every tread or reveal outlines through nearer geometry. It removes the stacked-box representation but does not meet an interpretation that requires every head-on tread to stay readable.

The final full PC-profile Play Mode review completed with exit code 0. The Mobile rendering-profile excavator review also completed with exit code 0 using D3D11. An earlier editor graphics run crashed during native shutdown after completing its captures; the final D3D11 run exited cleanly. This is Unity validation, not Android/device performance validation.

Six focused Editor checks passed: unreadable mesh participation/property-block preservation/inactive exclusion, idempotent setup with ConstructionSite and GamePoc, group/decal fade coverage, zero-fade decal rendering, and shader compilation/render state. Results: `Logs/OcclusionSilhouetteFocusedTests.xml`. At that validation date, the broader decal suite retained a stale body-renderer-count expectation (10 versus the current prefab's 12). Audit remediation now checks body/decal separation and real surface support without freezing body totals or world-space placement; authored decals remain unchanged. The old group-cache fixed renderer-count assertion was changed to compare against the actual prefab renderer count.

## Rendering cost

When no eligible renderers are fading, no silhouette pass is enqueued. With active silhouettes, cost is one extra actual-geometry draw per eligible visible submesh plus one fullscreen contour pass. There is no full camera-color copy. URP may also need a depth copy/prepass if the camera did not otherwise require a depth texture.

Observed counts from native 1280×720 gameplay frames (640×360 mask):

| Camera view | Mask draws | Submitted triangles |
| --- | ---: | ---: |
| Excavator | 11 | 105,130 |
| Stairs, across placements | 1–3 | 24–372 |
| Container and other simultaneously faded objects | 45 | 11,918 |
| Electrical substation | 1 | 2,120 |
| Building, across placements | 1–2 | 48–60 |

These are all active silhouettes in each frame, not isolated per-object draw counts. RG8 plus D16 targets request approximately 0.88 MiB at 720p and 1.98 MiB at 1080p, excluding backend alignment and any additional scene-depth storage. Half resolution reduces mask fill cost, not geometry processing. The excavator's 105k submitted triangles and the container area's 45 draws are material mobile costs. No phone GPU milliseconds or FPS claim is made; those need hardware profiling.

## Special handling / limits

- Strict coverage union does not show internal creases, hidden parts, or boundaries between overlapping faded objects. Head-on stairs and crowded silhouettes can therefore lose structural detail.
- Subpixel rails and holes can soften/disappear in the half-resolution mask. Existing fine dash settings can look stippled at small sizes.
- Custom vertex-displacement shaders need matching deformation in a mask pass; the generic override draws the supplied renderer geometry.
- Transparent materials use geometry coverage. Custom alpha clipping, texture animation/property-block texture overrides, and materials combining authored cutouts with `_Fade` need explicit mask support. `_Fade` dither is intentionally excluded from mask coverage.
- MeshRenderer and SkinnedMeshRenderer are supported; particles, trails, Terrain, and other non-mesh renderers are not represented by this pass.
- LOD crossfade/selection and animated skinned meshes were not visually validated in this environment pass.
- This feature targets the project's current URP RenderGraph configuration. Legacy compatibility mode and XR require separate support/validation.

## Recheck checklist

1. Open ConstructionSite, start Play Mode, move Amy behind the excavator, stairs/building, container and substation.
2. Check contour follows mesh perimeter, no bounds boxes/internal triangle lines, and fade detection returns normally when Amy leaves.
3. Adjust line width/color/dash settings only if desired; leave gameplay sampling/fade settings intact.
4. Run setup/repair twice and confirm one silhouette feature in each production renderer.
5. Profile mask draws and GPU time on target hardware before setting a mobile performance budget.

## Verified occlusion ownership and cache contract

The gameplay camera/preset owners determine framing. `CameraOcclusionController` owns
logical blocker fading and visibility semantics; the silhouette renderer feature consumes
its state. Render-only camera feedback remains separate from gameplay aiming.

Current detection uses ten context rays plus five Amy/body samples, with authored
thresholds and logical groups. Collider membership, source materials and group height
are cached at startup. Runtime-added/reparented blockers are currently unsupported;
there is no dynamic membership refresh. Material copies are created lazily, reused and
cleaned up on destruction; the approved visual behavior is retained.

The most recently enabled controller owns `Active`. Disabling removes it; reenabling
restores ownership; destroying a newer duplicate falls back to the previous enabled
survivor. Normal production uses one controller. Disable restores its faded materials;
actual render appearance still requires graphics/device verification.
