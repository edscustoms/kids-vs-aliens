# Loadout, Fighting access, reset and modal integration ? 19 September 2026

## Changes

- Fighting is an assignable **learned combat capability** in Inventory > Combat. Availability reads the existing PlayerSkillState and PlayerMeleeController.defaultCombatItem. It never enters the 25-item backpack. The existing Fighting icon catalog mapping supplies the category, details and quick-slot/HUD icons.
- Quick slots remain five assignment references. Nonnegative integers reference physical inventory entries; -1 remains empty; -2 represents the authored unarmed capability. Selecting it uses the existing SelectCombatItem path to holster the firearm. Selecting a gun equips its still-owned inventory entry.
- The ghost starting gun came from PlayerEquipment.Start equipping the menu selection directly without adding it to PlayerInventory. Fresh initialization now ensures exactly one owned starting gun before equipping; normal insertion assigns the first empty quick slot. An explicit None stays empty.
- Continue skips starting-loadout initialization. Saved physical items, assignments and active combat selection restore through the existing save flow. Earlier physical Fighting entries migrate out of backpack storage, preserving their assignment when learned. Earlier equipped ghost guns are adopted once into inventory on restore.
- Assigned physical items remain visible in the backpack, with dimmed icons and a SLOT indicator. Clearing an assignment restores their normal appearance.
- InGameMenuRoot was inside the HUD SafeArea; every stretched descendant backdrop inherited that inset. It now sits directly under the presentation Canvas. Knowledge Log also uses that full Canvas. Knowledge Acquired already had a full-Canvas dimmer and retains it. The central menu setup helper repairs existing scenes idempotently; GamePoc and ConstructionSite were repaired.
- Options / Settings now includes Reset Game Progress, followed by a danger confirmation with Cancel and Reset Everything. Confirmation quiesces the old active-run writer, tombstones both active-run generations, writes an empty permanent record to both permanent generations, clears runtime progression caches, and returns to Menu. Cancel only closes the confirmation. Hard Restart is unchanged.
- Reset deletes active-run state, learned Knowledge, skill XP/proficiency and permanent unlocks (currently stored in PermanentSave.skills). It preserves PlayerPrefs and all camera/audio/control/graphics settings. No PlayerPrefs.DeleteAll or arbitrary JSON deletion is used.
- Serialized save classes/version remain unchanged. The existing quickSlots integer representation gains the -2 capability token. Older valid assignments still load; this is not a downgrade guarantee for older app binaries.

## Validation

- 23 focused Unity Editor tests passed: inventory ownership/capacity, virtual capability eligibility/migration/round-trip, shared grenade storage, existing run persistence, and full reset with either recoverable generation corrupted.
- Real Play Mode sequence passed: menu-selected pistol -> fresh arrival -> book learning/tutorial -> repeated pistol/Fighting switching and existing attack -> Inventory Combat assignment -> saved Fighting choice -> Menu/Continue -> exact restored items and assignments -> reset cancel -> confirmed reset -> permanent disk/cache reload -> settings retained -> fresh None loadout.
- Fullscreen Canvas corner assertions passed for Pause, Inventory and Knowledge Acquired. Screenshots reviewed under Logs/ProceduralUI/loadout-*.png.
- Android development build and smoke test passed on OPPO CPH2493, 2772 x 1240 landscape. Touch menu-selected fresh pistol appeared in inventory and slot 1; touch Combat assignment and repeated Fighting/pistol switching worked. Pause, Inventory, Knowledge Log and Knowledge Acquired backdrops were visually inspected at all edges with no exposed strip. App log contained no Unity exceptions/errors. Saved test state confirmed exactly one owned pistol and [0, -2, -1, -1, -1] quick slots. All four original device save-generation files were restored byte-for-byte after testing; the updated app is left at Menu. No lifecycle marathon or progression reset was performed on the phone. Screenshots/logs are under Logs/LoadoutAndroid/.

## Files

Runtime: PlayerInventory.cs, PlayerEquipment.cs, PlayerMeleeController.cs (read-only default capability accessor only), InventoryUI.cs, ActiveRunController.cs, PermanentProgress.cs, RunSaveService.cs, InventoryManagementView.cs, GameplayInterface.cs, OptionsScreenController.cs, new ProgressResetView.cs.

Authoring: InGameMenuSetup.cs; GamePoc.unity and ConstructionSite.unity (menu root parent only).

Validation: LoadoutIntegrationTests.cs, LoadoutIntegrationReview.cs; updated InventoryCleanupTests.cs and explicit small-capacity fixture in PlayerInventoryGrenadeTests.cs. RunInterfaceValidation.cs now expects the owned starting loadout after Hard Restart instead of an empty inventory.

## Limits

An old save with an equipped ghost gun **and all 25 physical positions occupied** cannot adopt an additional gun without exceeding capacity; restore reports the problem and preserves the snapshot rather than discarding an owned item. No unrelated combat animations, hit timing, movement, Beam Hoist, camera or procedural renderer changes were made.
