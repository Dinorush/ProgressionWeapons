using CellMenu;
using Gear;
using HarmonyLib;
using Player;
using ProgressionGear.GearToggle;
using ProgressionGear.ProgressionLock;
using ProgressionGear.Utils;
using System.Collections.Generic;

namespace ProgressionGear.Patches
{
    [HarmonyPatch]
    internal static class GearManagerPatches
    {
        [HarmonyPatch(typeof(GearManager), nameof(GearManager.SetupGearInOfflineMode))]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_SetupGearInOfflineMode()
        {
            GearLockManager.Current.VanillaGearManager = GearManager.Current;

            foreach (var (inventorySlot, loadedGears) in GearLockManager.Current.GearSlots)
            {
                foreach (GearIDRange gearIDRange in GearManager.Current.m_gearPerSlot[(int)inventorySlot])
                {
                    uint playerOfflineDBPID = gearIDRange.GetOfflineID();
                    loadedGears.TryAdd(playerOfflineDBPID, gearIDRange);
                }
            }
            GearToggleManager.Current.ResetToggleInfos();
        }

        // Bug fix: Prevent null ref spam
        private readonly static Dictionary<uint, GearIDRange> _checksumToRanges = new();
        [HarmonyPatch(typeof(CM_PlayerInventory), nameof(CM_PlayerInventory.UpdateInventory))]
        [HarmonyWrapSafe]
        [HarmonyPrefix]
        private static void Pre_UpdateInventoryIcons()
        {
            _checksumToRanges.Clear();
            static void CacheSlot(InventorySlot slot)
            {
                var ranges = GearManager.GetAllGearForSlot(slot);
                foreach (var range in ranges)
                    _checksumToRanges.TryAdd(range.GetChecksum(), range);
            }

            CacheSlot(InventorySlot.GearStandard);
            CacheSlot(InventorySlot.GearSpecial);
            CacheSlot(InventorySlot.GearMelee);
            CacheSlot(InventorySlot.GearClass);
        }

        [HarmonyPatch(typeof(GearManager), nameof(GearManager.CheckGearIconTargetCallbacks))]
        [HarmonyWrapSafe]
        [HarmonyPrefix]
        private static bool Pre_IconsLoadedCallbacks(uint gearKey)
        {
            return !_checksumToRanges.TryGetValue(gearKey, out var range) || range != null;
        }
    }
}
