# KVA Smart Fence V2.3 — horizontal rotation fix

This fixes the actual geometry bug where extending from a reversed/left-facing
vertical fence could create the new section underneath the floor.

Root cause:

`Quaternion.FromToRotation(Vector3.right, direction)` is ambiguous for a 180°
turn (Right -> Left). Unity may solve that as a 180° rotation around Z instead
of a yaw around Y.

That flips local Y:
- rails go below the floor;
- chain-link goes below the floor;
- collider orientation is wrong.

V2.3 now builds every segment rotation with:
- a horizontal direction only;
- `Vector3.up` explicitly preserved;
- yaw-only orientation through `Quaternion.LookRotation`.

Result:
- +X / -X / +Z / -Z segments all remain upright;
- straight/turn extensions never flip under the floor;
- existing FenceRuns do not need regeneration.

Install by overwriting V2.2 scripts with V2.3.
