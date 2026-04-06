using ProgressionGear.Utils;
using Gear;
using Player;
using System.Collections.Generic;
using System.Linq;
using ProgressionGear.Dependencies;

namespace ProgressionGear.ProgressionLock
{
    public sealed class GearLockManager
    {
        public static GearLockManager Current { get; private set; } = new();

        public GearManager? VanillaGearManager { internal set; get; } // setup in patch: GearManager.SetupGearInOfflineMode


        private readonly HashSet<uint> _lockedGearIds = new();

        internal readonly List<(InventorySlot inventorySlot, Dictionary<uint, GearIDRange> loadedGears)> GearSlots = new() {
            (InventorySlot.GearStandard, new()),
            (InventorySlot.GearSpecial, new()),
            (InventorySlot.GearMelee, new()),
            (InventorySlot.GearClass, new()),
        };

        public void Init()
        {
            MTFO.API.MTFOHotReloadAPI.OnHotReload += SetupAllowedGearsForActiveRundown;
        }

        private void ConfigRundownGears()
        {
            _lockedGearIds.Clear();
            GearToggleManager.Current.ResetToggleInfos();
            if (Configuration.DisableProgression)
                return;

            // Acquire all progression-locked/-unlocked weapons
            IEnumerator<ProgressionLockData> dataEnum = ProgressionLockManager.Current.GetEnumerator();
            Dictionary<uint, PriorityLockData> priorityIDs = new();
            while (dataEnum.MoveNext())
            {
                ProgressionLockData progData = dataEnum.Current;
                foreach (uint id in progData.OfflineIDs)
                {
                    PriorityLockData lockData = new()
                    {
                        priority = progData.Priority,
                        locked = IsGearLocked(progData),
                        explicitLock = IsLockExplicit(progData)
                    };

                    if (priorityIDs.TryGetValue(id, out var data))
                    {
                        // Resolve lock conflict by explicit vs implicit locks or, if both are the same, by priority.
                        if ((data.explicitLock && !lockData.explicitLock) || data.priority > lockData.priority)
                            continue;
                    }

                    priorityIDs[id] = lockData;
                }
            }

            // Store locked IDs to remove from gear lists later
            foreach (var kv in priorityIDs.Where(kv => kv.Value.locked))
            {
                _lockedGearIds.Add(kv.Key);
                GearToggleManager.Current.RemoveFromToggleInfos(kv.Key);
            }
        }

        private struct PriorityLockData
        {
            public int priority;
            public bool locked;
            public bool explicitLock;
        }

        private void ClearLoadedGears()
        {
            foreach (var slot in GearSlots)
            {
                VanillaGearManager!.m_gearPerSlot[(int)slot.inventorySlot].Clear();
            }
        }

        public bool IsGearAllowed(uint id)
        {
            return !_lockedGearIds.Contains(id);
        }

        // Modifies the gear in each slot to only unlocked gear.
        // Appears to only affect loadouts. Excluded gear still remains in GearManager's registered data for everything.
        private void AddGearForCurrentRundown()
        {
            foreach (var (inventorySlot, loadedGears) in GearSlots)
            {
                var vanillaSlot = VanillaGearManager!.m_gearPerSlot[(int)inventorySlot];

                if (loadedGears.Count == 0)
                {
                    DinoLogger.Debug($"No gear has been loaded for {inventorySlot}.");
                    continue;
                }

                foreach (uint id in loadedGears.Keys)
                {
                    if (!EOSWrapper.IsGearAllowed(id))
                    {
                        GearToggleManager.Current.RemoveFromToggleInfos(id);
                    }
                    else if (IsGearAllowed(id))
                    {
                        vanillaSlot.Add(loadedGears[id]);
                    }
                }

                if (vanillaSlot.Count == 0)
                {
                    DinoLogger.Warning($"No gear is allowed for {inventorySlot}!");
                }
            }
        }

        private void ResetPlayerSelectedGears()
        {
            VanillaGearManager!.RescanFavorites();
            foreach (var (inventorySlot, _) in GearSlots)
            {
                var inventorySlotIndex = (int)inventorySlot;

                try
                {
                    if (VanillaGearManager.m_lastEquippedGearPerSlot[inventorySlotIndex] != null)
                        PlayerBackpackManager.EquipLocalGear(VanillaGearManager.m_lastEquippedGearPerSlot[inventorySlotIndex]);
                    else if (VanillaGearManager.m_favoriteGearPerSlot[inventorySlotIndex].Count > 0)
                        PlayerBackpackManager.EquipLocalGear(VanillaGearManager.m_favoriteGearPerSlot[inventorySlotIndex][0]);
                    else if (VanillaGearManager.m_gearPerSlot[inventorySlotIndex].Count > 0)
                        PlayerBackpackManager.EquipLocalGear(VanillaGearManager.m_gearPerSlot[inventorySlotIndex][0]);
                }
                catch (Il2CppInterop.Runtime.Il2CppException e)
                {
                    DinoLogger.Error("Error attempting to equip gear for slot " + inventorySlot + ":\n" + e.StackTrace);
                }
            }
        }

        // Must be done separate from AddGearForCurrentRundown so that ResetPlayerSelectedGears can still see all allowed gear.
        private void RemoveToggleGears()
        {
            foreach (var (inventorySlot, _) in GearSlots)
            {
                var gearList = VanillaGearManager!.m_gearPerSlot[(int)inventorySlot];

                for (int i = gearList.Count - 1; i >= 0; i--)
                    if (!GearToggleManager.Current.IsVisibleID(gearList[i].GetOfflineID()))
                        gearList.RemoveAt(i);
            }
        }

        internal void SetupAllowedGearsForActiveRundown()
        {
            if (!ProgressionWrapper.UpdateReferences()) return;

            EOSWrapper.CacheLocks();
            ConfigRundownGears();
            ClearLoadedGears();
            AddGearForCurrentRundown();
            ResetPlayerSelectedGears();
            RemoveToggleGears();
        }

        // If there are unfinished unlock requirements, it is implicitly locked.
        // If unlock requirements are met, it can still be explicitly locked by lock requirements.
        private static bool IsGearLocked(ProgressionLockData data)
        {
            bool locked = (data.Unlock.Any() && !IsComplete(data.Unlock, data.UnlockRequired, data.MissingLevelDefault))
                       || (data.Lock.Any() && IsComplete(data.Lock, data.LockRequired, data.MissingLevelDefault));

            return locked;
        }

        // Returns whether the lock (or unlock) for some data is explicitly set due to fulfilled requirements.
        // Conversely, an implicit lock occurs when unlock requirements are set but no requirements are fulfilled.
        private static bool IsLockExplicit(ProgressionLockData data)
        {
            return IsComplete(data.Unlock, data.UnlockRequired, data.MissingLevelDefault)
                || (data.Lock.Any() && IsComplete(data.Lock, data.LockRequired, data.MissingLevelDefault));
        }

        private static bool IsComplete(List<ProgressionRequirement> list, int require, bool valueIfNone)
        {
            if (require == 0)
            {
                foreach (var req in list)
                    if (!IsComplete(req, valueIfNone))
                        return false;
            }
            else
            {
                foreach (var req in list)
                {
                    if (IsComplete(req, valueIfNone))
                        require--;

                    if (require == 0) break;
                }
            }
            return require == 0;
        }

        private static bool IsComplete(ProgressionRequirement req, bool valueIfNone) => ProgressionWrapper.IsComplete(req, valueIfNone);
    }
}