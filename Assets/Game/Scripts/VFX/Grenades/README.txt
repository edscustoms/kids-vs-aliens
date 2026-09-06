Kids VS Aliens - Electric Grenade VFX V5.5 (Final V1 Cleanup)

This is the version to park until the game-wide V1 polish pass.

Changes from V5.4:
- Energy cloud now emits from several offset pockets instead of one centered puff.
- Cloud still uses only TWO particle systems (CloudBody + BrightWisps).
- Bright wisps use stretched rendering so some energy pulls outward with the discharge.
- Cloud body remains visible after the shock ring fades, within the existing 0.85s presentation duration.
- Obsolete Ionized Mist field/code/setup path removed.
- Setup explicitly writes all final cloud + lightning authored values.
- Existing V5.4 lightning/core/ring/gameplay behavior is otherwise unchanged.

Install:
1. Replace ElectricArcVFX.cs
2. Replace ElectricGrenadeBurstVFX.cs
3. Replace ElectricGrenadeVfxSetup.cs
4. Replace ElectricEnergyCloudVFX.cs
5. Keep your existing .meta files when Unity already has these scripts.
6. Run: Tools > Kids VS Aliens > Setup > Electric Grenade VFX V5

No manual prefab wiring should be required after setup.
