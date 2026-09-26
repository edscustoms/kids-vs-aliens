# Healing Pod proximity prototype

`Assets/Game/Prefabs/Environment/PF_HealingPod.prefab` owns the prototype. Its
`InteractionTrigger/HealingPodController` recognizes the existing player
`PlayerCharacter`, tracks overlapping colliders and animates explicit visual
references. `Visual` contains the nested FBX; `Collision` contains six simple boxes.
The trigger has its own kinematic Rigidbody and does not block movement.

The controller captures the prefab's closed pose once in Awake. A single normalized
progress value drives absolute door offsets and a hinge rotation, so reversal does
not accumulate transform drift. Scaled time respects world pause. Disabled or
destroyed colliders are removed; trigger Stay handles reenable inside the radius.
Opening is derived from proximity, not saved run state. No healing, floating,
interaction input, animation clips or resource consumption is implemented.

## Authoring / replacing the visual

Replace the `Visual` child, author its closed pose, and assign the two door transforms
and roof transform on `InteractionTrigger`. Door offsets use each door's parent-local
space; the roof axis is relative to its closed local rotation. Put the roof origin
at its rear hinge and keep the indicator beneath it. The controller never searches
for imported object names. Keep gameplay collision and the trigger outside `Visual`.
Meshes reference the FBX; the four placeholder URP materials are shared assets.

The current FBX is authored open. Prefab transform overrides close it. Unity's axis
conversion mirrors its X coordinates and retains the conversion on the imported
root; do not reset that root rotation. Import uses meter units, preserved hierarchy
and no animation import.

| Setting | Value |
| --- | --- |
| Trigger radius / center above prefab root | 3 m / 1 m |
| Full open or close duration | 0.7 s |
| Left door closed local position | (0.49, -0.72, 0.29) |
| Right door closed local position | (-0.49, -0.72, 0.29) |
| Left / right open offsets | (0.55, -0.08, 0) / (-0.55, -0.08, 0) |
| Roof closed / open rotation | Imported closed pose / -75 degrees around local X |

ConstructionSite's test instance is grounded 5 m right and 2 m forward of its authored
LevelStart and faces the spawn. This is explicit scene content, not a required shared
scene dependency or part of routine scene repair.

Focused validation (the filter runs only the pod fixture):

```powershell
.\Tools\Run-UnityTests.ps1 -Suite Full -ReuseCopy -TestFilter 'HealingPodTests'
```

`HealingPod-02`: 3/3 passed on Unity 6000.5.6f1, including two Play Mode scenarios.
Tests cover prefab references, real physics entry/exit and
interruption, disabled/multiple colliders, and ConstructionSite arrival and walking
through the chamber and back to the opening route. Device feel remains a manual check.
