Beam Transport V2

Historical helper/file lists below record earlier implementation passes. Some one-off tools are now retired; see [helper cleanup](../../../Docs/LegacyHelperCleanup.md). Use the [Tests README](../Tests/README.md) for current validation commands.

BeamTransportController is the single owner of beam movement, the
GameplaySuspensionController transport lease, CharacterController handoff, route
validation, reusable runtime VFX and transport teardown.

Arrival, departure and the Knowledge-gated Beam Hoist all use this controller.

The current hoist is one continuous cubic Bézier path from Amy's start position
to the final landing position. There is no separate start -> release movement
phase.

Runtime architecture

BeamTransportController

BeamTransportController owns:

arrival movement,

Beam Hoist movement,

departure movement,

gameplay suspension ownership,

CharacterController disable/restore,

route collision checks,

landing validation,

the reusable runtime PF_BeamTransportVFX instance,

cleanup/interruption.

For Beam Hoist, the controller runs exactly one movement segment:

START (Amy on ground)
↓
BeamHoistPath.Evaluate(0 -> 1)
↓
LANDING

release, control1 and control2 are curve-shaping/clearance data only.
They are not separate movement destinations.

During Beam Hoist, the beam follows Amy horizontally:

Beam X = Amy X
Beam Z = Amy Z
Beam Y = original beam root Y

This keeps the beam visually centred under Amy while preserving its grounded
vertical presentation.

The beam remains visible for the complete hoist and is hidden when the validated
landing is reached and control is restored.

Arrival and departure still use normal straight transport paths.

BeamHoistPath

BeamHoistPath is a snapshot of one validated route.

The cubic Bézier is:

P0 = start
P1 = control1
P2 = control2
P3 = landing

Therefore:

Evaluate(0) = start
Evaluate(1) = landing

The current path shape keeps the first handle above Amy and the second handle
above the landing, producing a strong upward start with a smooth continuous arc
toward the destination.

release remains metadata representing the safe/desired curve height and may be
used by limits, authoring and debug tooling. The controller must never move Amy
to release as a separate phase.

The complete curve is capsule-validated before activation and swept again while
moving.

Beam Transport VFX

The canonical beam prefab is:

Assets/Game/Prefabs/PF_BeamTransportVFX.prefab

BeamTransportVFX.SetDirection(Up/Down) switches the authored BeamSparks
Velocity-over-Lifetime vertical direction from a cached baseline without changing
the rest of the particle authoring.

ConstructionSite's BeamSparks emitter is rotated, so direction handling is based
on the actual authored/world movement rather than assuming local +Y means world
up.

The beam preserves its authored:

particle size,

colour,

shape,

emission,

sideways drift,

renderer/material setup.

The outer cone is no longer dependent on transient ProBuilder render geometry.
Its authored mesh was baked to:

Assets/Game/Generated/BeamOuterCone.asset

The canonical prefab uses that persistent mesh.

Arrival and runtime hoists therefore use the same canonical visual content.

Current visual behaviour

Arrival: beam is placed at the authored LevelStart position and transports Amy
downward.

Hoist: beam starts under Amy and follows her X/Z throughout the one continuous
curve.

Departure: beam transports Amy upward.

Hoist beam Y stays rooted to its original ground level.

Beam is hidden when the hoist lands.

Later visual polish

Not required for the current implementation:

gradual beam fade-in/fade-out,

a gameplay ground indicator showing valid Beam Hoist activation areas.

LevelStart prefab

The reusable LevelStart template is:

Assets/Game/Prefabs/PF_LevelStart.prefab

It contains:

PF_LevelStart
├── PlayerBeamInSequence
├── BeamInSpawn
└── PF_BeamTransportVFX

The prefab owns the standard structure and wiring.

Scene instances own their placement and authored overrides such as:

LevelStart position/rotation,

BeamInSpawn placement,

start height,

initial delay,

descent duration,

landing hold.

The ConstructionSite migration preserved the authored hierarchy transforms,
active states and arrival timing values.

Routine repair must not silently snap/move an authored arrival marker. Invalid
placement should be reported and corrected explicitly in the Scene view.

PlayerBeamInSequence is only the startup adapter. It requests arrival through
BeamTransportController during startup and does not run a delayed pose-reset
coroutine.

Scene setup / repair

Use:

Tools > Setup > Setup or Repair Active Gameplay Scene

The setup/repair flow wires:

BeamTransportController,

BeamHoistAbility,

Beam Hoist Knowledge requirement,

canonical PF_BeamTransportVFX,

PlayerBeamInSequence,

PF_LevelStart / arrival references.

The current setup can instantiate PF_LevelStart when a gameplay scene has no
existing LevelStart.

If a scene should not use automatic beam arrival, removing the LevelStart removes
arrival behaviour; running the central repair again may recreate the standard
LevelStart template.

The old generated standalone HOIST touch button is retired/disabled. Production
input uses contextual Jump.

Knowledge and input

Beam Hoist uses the existing progression system:

BeamHoist.asset

BeamHoistBook.asset

BeamHoistBook_Dropped.prefab

PlayerSkillState

Automatic level arrival/departure do not require Beam Hoist Knowledge.

Manual Beam Hoist does.

Production input

The normal Jump input is contextual:

Jump pressed
↓
valid Beam Hoist candidate + Knowledge unlocked
→ Beam Hoist consumes the press

otherwise
→ normal Jump

This applies to desktop and mobile because both use the normal Starter Assets
Jump input path.

Keyboard H may remain available as a debug/legacy Beam Hoist request, but the
production interaction is Jump.

After landing on top of a hoistable object, Jump returns to ordinary jumping
because the top is not a valid lower-side activation region.

Fresh-input/neutral handling remains important so a consumed hoist press does not
turn into an accidental jump immediately afterward.

Smart hoist authoring

For normal rectangular/static props, use:

BeamHoistSurface

Add it to the root GameObject of the object Amy should be able to hoist onto.

Examples:

shipping containers,

suitable platforms,

raised construction props,

other upright static ledges with valid collision.

Normal authoring should not require helper Hoist / Landing child objects.

BeamHoistSurface baker

BeamHoistSurfaceBaker performs the expensive geometry work in the Editor.

It:

gathers enabled solid static colliders,

determines the prop bounds,

finds supported top points,

creates cached lower-side approach cells,

samples nearby support floors for props standing on raised foundations/stacks,

excludes the top from activation,

stores the bake result on the component,

provides Scene/Inspector feedback,

rebakes for builds.

Runtime does not analyze the full geometry every frame.

At activation time it evaluates cached candidates, ability limits and real physics.

Standard BeamHoistSurface fields

Typical fields include:

Source Colliders,

Approach Width,

Landing Inset,

Clearance,

optional Landing Override,

baked approach/landing data.

Leaving Source Colliders empty lets the baker use suitable child colliders.

For tilted/irregular structures that do not fit the standard smart baker,
BeamHoistTarget remains available as an authored/manual fallback.

Ability tuning

The main global tuning lives on the Player's BeamHoistAbility.

Current exposed controls are:

Ability Limits

Minimum Vertical Gain

Minimum difference between:

landing.y - start.y

A landing below this gain is not considered a Beam Hoist.

Example:

Minimum Vertical Gain = 0.6

means the landing must be at least 0.6 m above Amy.

Do not set this above Maximum Hoist Height, otherwise no route can satisfy both
limits.

Maximum Hoist Height

Maximum permitted landing height relative to Amy's start position.

This limits what Amy is allowed to reach; it is not the visual arc height.

Maximum Lateral Distance

Maximum horizontal distance between Amy's start and the landing.

Movement

Hoist Duration

Total duration of the complete single START -> LANDING Bézier.

There is no separate lift timer plus transfer timer for smart surfaces anymore.

Arc Height Above Landing

Desired visual curve height above the final landing.

The path still respects the minimum safe clearance supplied by the hoist surface:

safeHeight = max(
required surface clearance,
landing.y + Arc Height Above Landing
)

So lowering Arc Height cannot force Amy through geometry.

Future improvement

Arc Height Above Landing is currently a global Player ability setting.

Longer term it should support an optional per-BeamHoistSurface override so
different objects can use different silhouettes, for example:

3 m container -> smaller arc
9 m structure -> larger/custom arc

The Player value can remain the default fallback.

Physics and safety

Before Beam Hoist starts:

Amy must currently be on a valid supported pose,

Knowledge must be unlocked,

the approach candidate must be active,

landing gain must satisfy minimum/maximum limits,

lateral distance must be within the ability limit,

landing must support the full player capsule,

the complete Bézier must pass capsule collision validation.

The curve is sampled and swept with the player capsule rather than point-only
raycasts.

During movement, the traversed part of the curve is validated again so new
blocking geometry can interrupt the transport safely.

On interruption:

transport stops,

beam hides,

original CharacterController enabled state is restored,

motion is reset,

the Beam Transport suspension lease is released.

Physics-query overflow rejects movement conservatively.

BeamHoistTarget fallback

BeamHoistTarget remains available for irregular/manual traversal cases.

It should not be the standard authoring workflow for normal containers/platforms.

If used, it can provide authored:

activation range/volume,

landing transform,

lift/clearance information,

timing.

BeamTransportController.TryBuildHoist() converts it into the same
BeamHoistPath representation used by the transport system.

Prefer BeamHoistSurface unless the geometry needs explicit manual authoring.

Level exits and interruption

LevelBeamTransportTrigger can request departure transport and invoke the level's
existing transition flow.

A completed departure can keep control owned until scene transition/unload.

An explicit CancelTransport() releases the Beam Transport lease if a transition
is cancelled.

Beam Transport suspension blocks gameplay input/consumers without globally
freezing time.

Pause/Knowledge modal ownership remains independent: one owner releasing its lease
must not release another owner's control lock.

Current ConstructionSite state

ConstructionSite has been migrated to the V2 architecture.

Current validated behaviour includes:

LevelStart arrival owns Amy before normal gameplay Update,

automatic arrival uses the canonical full beam,

BeamOuterCone renders from the baked mesh,

Beam Hoist Knowledge gating,

smart BeamHoistSurface authoring on the test container,

lower-side activation rather than spherical/top activation,

top/landing reactivation rejection,

contextual Jump activation,

normal Jump fallback when no valid hoist exists,

one continuous cubic START -> LANDING path,

beam following Amy on X/Z through the hoist,

exact supported landing,

collision rejection,

ability height/lateral limits.

The smart container baker also handles the tested stacked-container case by using
the nearby supporting floor instead of assuming the prop's own bottom is the
player's approach floor.

Acceptance checks

Arrival

Enter ConstructionSite Play Mode while holding movement/FIRE.

Verify Amy is owned before normal gameplay input can move her.

Verify one clean descent.

Verify no delayed reset/second descent.

Verify the full canonical cone/core/middle/sparks/ring appear.

Verify downward particle direction.

Verify controls restore after landing.

Beam Hoist

Unlock Beam Hoist Knowledge.

Stand in a valid lower-side approach region.

Press normal Jump on desktop and mobile.

Verify Beam Hoist consumes Jump only when the route is valid.

Verify the path begins at Amy's current ground position.

Verify there is one path only: no initial separate vertical movement, reset
or teleport.

Verify the curve ends exactly at the supported landing.

Verify the beam follows Amy horizontally for the complete hoist.

Verify the beam remains grounded in Y.

Verify the beam hides on landing.

Verify Jump works normally after landing.

Verify standing on the top cannot reactivate the same hoist.

Verify blocked curve/landing requests fall back safely.

Limits

Test:

below Minimum Vertical Gain,

above Maximum Hoist Height,

beyond Maximum Lateral Distance,

blocked route,

unsupported landing.

Remember:

Minimum Vertical Gain <= valid landing gain <= Maximum Hoist Height

Suspension / lifecycle

Pause during transport.

Resume transport.

Open/close Knowledge UI during relevant gameplay states.

Disable/unload during transport.

Verify other suspension owners remain isolated.

Device

Manually verify Android/iOS:

contextual Jump,

Beam Hoist consumption,

normal Jump fallback,

lifecycle/pause,

visual beam tracking,

performance.

Tests / verification

Automated coverage exists in:

BeamTransportTests

BeamTransportV2Tests

The earlier V2 implementation reached 33/33 focused Beam Transport checks
passing before the final manual path/beam-follow tuning.

A later manual Test Runner run showed the beam-related fixtures green; unrelated
ExcavatorDecalTests failures remained elsewhere in the Core run.

Important: the final manual changes changed previously asserted V2 presentation
behaviour:

hoist is now one START -> LANDING Bézier,

beam now follows Amy horizontally,

beam no longer switches off before the curved transfer.

Any old test/review assertions expecting:

vertical phase -> release -> curve
stationary beam
beam off before curve

must be updated before treating the complete automated suite as current.

Run the Beam Transport fixtures again after those assertions are updated, then run
the full Core regression suite separately.

Do not reuse the old 110 passed V1 result as proof of the final V2 behaviour.

Key assets / files

Runtime

Assets/Game/Scripts/Gameplay/BeamTransportController.cs

Assets/Game/Scripts/Gameplay/BeamTransportVFX.cs

Assets/Game/Scripts/Gameplay/BeamHoistAbility.cs

Assets/Game/Scripts/Gameplay/BeamHoistSurface.cs

Assets/Game/Scripts/Gameplay/BeamHoistPath.cs

Assets/Game/Scripts/Gameplay/BeamHoistTarget.cs

Assets/Game/Scripts/Gameplay/PlayerBeamInSequence.cs

Assets/Game/Scripts/Gameplay/LevelBeamTransportTrigger.cs

Assets/StarterAssets/InputSystem/StarterAssetsInputs.cs

ThirdPersonController.cs

GameplaySuspensionController.cs

Editor / setup

Assets/Game/Editor/Helpers/BeamHoistSurfaceBaker.cs

Assets/Game/Editor/Helpers/BeamTransportSetup.cs

Assets/Game/Editor/Helpers/BeamTransportPrefabMigration.cs

Assets/Game/Editor/Helpers/BeamTransportReview.cs

Assets/Game/Editor/Helpers/BeamTransportV2Review.cs

Assets/Game/Editor/Helpers/BeamTransportV2PlayReview.cs

Assets/Game/Editor/GameplaySceneSetup.cs

Prefabs / data

Assets/Game/Prefabs/PF_BeamTransportVFX.prefab

Assets/Game/Prefabs/PF_LevelStart.prefab

Assets/Game/Generated/BeamOuterCone.asset

Assets/Game/Data/Progression/BeamHoist.asset

Assets/Game/Data/Items/KnowledgeBooks/BeamHoistBook.asset

Assets/Game/Prefabs/Items/KnowledgeBooks/BeamHoistBook_Dropped.prefab

Scene

Assets/Game/Scenes/ConstructionSite.unity

Tests

Assets/Game/Tests/Editor/Core/BeamTransportTests.cs

Assets/Game/Tests/Editor/Core/BeamTransportV2Tests.cs

Unity .meta files for new assets are part of the implementation.

Short current contract

Knowledge unlocked
↓
BeamHoistSurface on prop
↓
Editor-baked lower-side approach cells + supported top landing candidates
↓
Player enters a valid lower approach
↓
Jump
↓
Ability validates limits + full capsule route
↓
ONE cubic Bézier:
START ─────────────────────────────→ LANDING
↓
beam follows Amy on X/Z
↓
supported landing
↓
beam hides + capsule/control restored

That is the current Beam Transport / Beam Hoist production contract.
