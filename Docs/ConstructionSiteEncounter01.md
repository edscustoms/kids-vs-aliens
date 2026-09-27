# ConstructionSite Encounter 01

`Encounter01_FirstMelee` is an instance of `PF_AuthoredEncounter` in ConstructionSite.
It owns an `AuthoredEncounter`, a nonblocking
box trigger and a stable `RunWorldObject`. Its three inactive scene children are
instances of `PF_Enemy_Melee_POC_V1`; their transforms and references remain editable.
There is no spawn loop, objective, reward or additional combat owner.
Reusable authoring, equipment selection and optional activation hooks are described
in [Gameplay authoring](GameplayAuthoring.md). Migration preserved its trigger,
three member poses/configuration and all existing stable identities.

The reference platform is the front/east first-floor room, bounded by
`First_South_East_Division` and `First_Utility_To_StairForecourt`. The existing floor
colliders put its surface at Y=8.2; the existing NavMesh samples at Y=8.22.
The trigger root is (-6.2, 8.2, 15.1), box center (0, 1.25, 0), size (8, 2.7, 7.6).
Its west boundary at X=-10.2 leaves 1.9 m after the entry partition at X=-12.1.
It requires Amy's root inside, not just her capsule touching the edge, and ignores
entry during Beam/input suspension or Active Run restoration.

| Enemy | World position | Facing yaw |
| --- | --- | --- |
| Encounter01_Alien_1 | (-9, 8.22, 21.5) | 270 degrees |
| Encounter01_Alien_2 | (-6, 8.22, 21.25) | 271.96 degrees |
| Encounter01_Alien_3 | (-9, 8.22, 24) | 239.83 degrees |

These positions sit beside the stair base/west railing. Existing NavMesh paths reach
the platform through the partition doorway. The scene instances have a 20-second
investigation allowance for that route; prefab combat/movement tuning is unchanged.
`AlienMeleeOnly.asset` retains melee and disables ranged equipment and scavenging.
There is no starting weapon or weapon drop. The Medium baseline remains 140 HP and
10 enemy melee damage; Amy's existing authored melee damage remains 30.

Activation enables the three existing instances and calls
`EnemyBrain.InvestigatePosition` once with Amy's position. This enters the existing
navigation/investigation behavior even before the enemies' first Start. Ordinary
perception/LOS then owns chase and attack; the encounter never drives combat itself.

The `authored-encounter` participant saves only `triggered`. Enemy health, removed
state, active state, equipment and awareness remain owned by the existing enemy/world
snapshot. Both restore passes assign the trigger flag without activating peers.
Completion derives from all three enemies being dead/removed (including corpses
already destroyed by normal death presentation). Continue neither respawns enemies
nor replays activation. Fresh attempts reload the authored dormant instances normally.

Focused coverage lives in `AuthoredEncounterTests` and `AuthoredEncounterPlayTests`:
placement/navigation, physical entry, suspension/edge rejection, direct engagement,
real Fighting damage, before/partial/completed Save/Continue, and Hard Restart.
Device combat feel, touch traversal and subjective timing remain manual checks.

Unity 6000.5.6f1: `Encounter01-Final` passed **126/126** (4 encounter + 122 adjacent
enemy, melee, Active Run/lifecycle, Beam and LevelStart checks), with no unexpected
Console errors. XML/logs are under `Logs/RepositoryAuditRemediation`. An initial NUnit
assertion compatibility issue and a legitimate out-of-contact jab in the new fixture
were corrected in tests; combat tuning was not changed. `git diff --check` passed.
All 2,488 pre-existing records outside the scene root list were preserved; the root
list only gained the new entry. The earlier user-authored enemy removal was retained. The temporary
scene authoring helper was removed. No full regression or device deployment was run.
