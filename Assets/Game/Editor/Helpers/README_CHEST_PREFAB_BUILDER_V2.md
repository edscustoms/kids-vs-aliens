# KVA Alien Chest Prefab Builder V2

## What V2 generates

One imported FBX creates TWO prefabs:

```text
World/
└── PF_AlienChest_POC_V1
    ├── ChestVisualRig
    ├── LootChest
    ├── BoxCollider
    ├── Visual
    └── ProximityHoldSensor
        ├── SphereCollider (Trigger)
        ├── ProximityHoldTrigger
        ├── ProximityProgressRing
        └── ChestProximityOpener

Menu/
└── PF_AlienChest_POC_V1_MenuPreview
    ├── ChestVisualRig
    └── Visual
```

The menu prefab intentionally has:
- no LootChest
- no collision
- no proximity trigger
- no interaction/gameplay logic

`ChestVisualRig` remains because it is a clean visual cache for LidPivot and rarity glow,
which is useful for future menu animation/rarity previews.

## Naming

Source:

```text
KVA_AlienChest_POC_V1.fbx
```

Generates:

```text
PF_AlienChest_POC_V1
PF_AlienChest_POC_V1_MenuPreview
```

`KVA_` is stripped automatically.

## Default transform

```text
Rotation Offset = (-90, 180, 0)
Model Scale = 0.60
```

Prefab roots remain `(1,1,1)`.

## Use

1. Select imported chest FBX in Project.
2. Open:
   `Tools > Kids VS Aliens > Helpers > Alien Chest Prefab Builder`
3. Click `Use Selected Model`.
4. Optional for a brand-new chest:
   drag `PlasmaPistol_Dropped` into `Initial Loot Prefab`.
5. Click:
   `Create / Update WORLD + MENU Prefabs`.

For an existing world chest, its existing LootChest settings are preserved on rebuild.

## One-time player setup

World chest proximity requires:

```text
ProximityInteractor
```

on the Player root. Only add this once to the player.

## Architecture

LootChest remains independent of proximity.

The WORLD prefab simply includes the generic proximity system by default because that is
the standard in-game chest behavior.

The MENU prefab uses only the visual wrapper.
