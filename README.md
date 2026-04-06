# ProgressionGear

Adds 2 features for rundown developers: **Gear Toggling** and **Progression-Locked Gear**. These are read in via custom files under `Custom/ProgressionGear/GearToggle/` and `Custom/ProgressionGear/ProgressionLocks/` respectively. If the directories do not exist, template files are created automatically.

## Gear Toggling

Gear Toggling allows gear to occupy the same slot in the loadout selection screen. When a gear with toggle data is selected, a button appears underneath its description that swaps to the next gear when clicked.

The custom files are lists of objects which contain:
- `OfflineIDs`: A list of PlayerOfflineGear IDs that are swapped between.
- `ButtonText`: The text to display on the button. Can specify two buttons in a list to move forward or backward in the list.
  - With two buttons, the right button follows single button direction while the left button follows the opposite.
- `ReverseOrder`: Reverses the direction buttons move through the list.
- `Name`: Serves no practical purpose, but can be handy for organizing/debugging as a developer.

The first ID in the list is the default weapon that appears; all others are hidden. Gear is swapped in the order of the list. The first ID in the list also determines the gear type (Main, Special, Tool, Melee). There are two rules for IDs:
- Unique: No ID can appear more than once across all lists.
- Same Slot: All IDs in the same list must be of the same gear type as the first ID.

If any IDs are progression locked, they are skipped when swapping weapons. If a list only has 1 or 0 valid IDs, no swap button will appear.

## Progression-Locked Gear

Progression-Locked Gear allows gear to be locked or unlocked as expeditions or tiers are completed. It is **not** used to lock gear to certain levels; [ExtraObjectiveSetup](https://thunderstore.io/c/gtfo/p/Inas07/ExtraObjectiveSetup/) offers that instead.

The custom files are lists of objects which contain:
- `Unlock`: A list of level layout IDs, tiers, levels, or full requirement objects that must be completed to unlock the gear.
  - Layout IDs can be numbers or PartialData string IDs.
  - Tiers can be in the formats "TierA" or "B".
  - Levels are specified by Tier + Index (by 1), e.g. "TierA1" or "B3".
- `UnlockRequired`: The number of unlock requirements that must be completed to unlock the gear. If 0, requires all of them.
- `Lock`: A list of level layout IDs, tiers, levels, or full requirement objects that must be completed to lock the gear.
- `UnlockRequired`: The number of lock requirements that must be completed to lock the gear. If 0, requires all of them.
- `OfflineIDs`: A list of PlayerOfflineGear IDs these unlocks/locks apply to.
- `Priority`: Specifies the priority of this unlock/lock. If two different blocks unlock/lock the same gear, the highest priority's unlock/lock is used.
- `Name`: Serves no practical purpose, but can be handy for organizing/debugging as a developer.

You may use a requirement object if you wish to specify sector completions. They contain the fields:

- `Level`: A level layout ID, tier, or level.
- `Main`, `Secondary`, `Overload`, `All`, `AllNoBoosters`: Requires the corresponding clear if set to true.
  - By default, only `Main` is required.

Any gear that has unlock requirements is automatically locked until they are completed. Additionally, lock requirements override unlock requirements in the event that both are completed.

Can work on mods with multiple rundowns, but has limited support. Requirements are only checked against the current rundown. If a level layout ID is not found, it is assumed to be completed.

Note: When resolving lock conflicts, completed requirements are considered a higher tier of priority. Empty unlock requirements are considered completed for these purposes. For instance, consider these two blocks:
```
{
  "UnlockTiers": ["TierA"],
  "LockTiers": ["TierB"],
  "Priority": 0
},
{
  "UnlockTiers": ["TierB"],
  "Priority": 1
}
```
Upon clearing A tier, the gear is unlocked since the first block's completed requirements give it a higher priority tier than the second. Once B tier is completed, the gear still remains unlocked since both blocks have the same priority tier and the second block has higher priority.
