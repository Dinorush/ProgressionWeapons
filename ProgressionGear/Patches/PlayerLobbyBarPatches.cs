using CellMenu;
using Gear;
using HarmonyLib;
using Player;
using ProgressionGear.Dependencies;
using ProgressionGear.ProgressionLock;
using ProgressionGear.Utils;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProgressionGear.Patches
{
    [HarmonyPatch]
    internal static class PlayerLobbyBarPatches
    {
        private static readonly Vector3 SingleButtonPos = new(-300, -320, -1);
        private static readonly Vector2 SingleColliderSize = new(290f, 56f);
        private static readonly Vector2 SingleButtonSize = new(270f, 47f);
        private static readonly (Vector3 left, Vector3 right) DoubleButtonPos = (new(-435, -320, -1), new(-165, -320, -1));
        private static readonly Vector2 DoubleButtonSize = new(270f * DoubleSizeMod, 47f);
        private static readonly Vector2 DoubleColliderSize = new(290f * DoubleSizeMod, 56f);
        private const float DoubleSizeMod = 0.8f;

        // In some cases (join in progress) SetActiveExpedition doesn't seem to be called? Hopefully this fixes it.
        [HarmonyPatch(typeof(CM_PlayerLobbyBar), nameof(CM_PlayerLobbyBar.ShowWeaponSelectionPopup))]
        [HarmonyAfter(EOSWrapper.GUID)] // EOS doesn't patch this, but futureproofing JFS
        [HarmonyWrapSafe]
        [HarmonyPrefix]
        private static void Pre_ShowLoadoutForSlot()
        {
            uint lastID = ProgressionWrapper.CurrentRundownID;
            if (!ProgressionWrapper.UpdateReferences() || lastID == ProgressionWrapper.CurrentRundownID) return;

            GearLockManager.Current.SetupAllowedGearsForActiveRundown();
        }

        private static CM_InventorySlotItem? _cachedItem;
        [HarmonyPatch(typeof(CM_PlayerLobbyBar), nameof(CM_PlayerLobbyBar.UpdateWeaponWindowInfo))]
        [HarmonyPrefix]
        private static void Pre_LoadoutHeaderSelected(CM_PlayerLobbyBar __instance)
        {
            _cachedItem = __instance.selectedWeaponSlotItem;
        }

        [HarmonyPatch(typeof(CM_PlayerLobbyBar), nameof(CM_PlayerLobbyBar.UpdateWeaponWindowInfo))]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_LoadoutHeaderSelected(CM_PlayerLobbyBar __instance, InventorySlot slot)
        {
            // If the player's selected weapon was already found, don't need to do anything.
            if (_cachedItem != __instance.selectedWeaponSlotItem) return;
            // Otherwise, they may have selected a weapon that isn't the default slot of its toggle list.
            // Need to check each slot item to find which one the weapon belongs to.

            if (!PlayerBackpackManager.TryGetItem(__instance.m_player, slot, out var bpItem) || bpItem.GearIDRange == null) return;
            
            uint bpID = bpItem.GearIDRange.GetOfflineID();
            if (!GearToggleManager.Current.TryGetToggleInfo(bpID, out var toggleInfo)) return;

            foreach (var content in __instance.m_popupScrollWindow.ContentItems)
            {
                CM_InventorySlotItem slotItem = content.TryCast<CM_InventorySlotItem>()!;
                if (toggleInfo.ids.Contains(slotItem.m_gearID.GetOfflineID()))
                {
                    slotItem.IsPicked = true;
                    slotItem.LoadData(bpItem.GearIDRange, true, true);
                    __instance.OnWeaponSlotItemSelected(slotItem);
                    _cachedItem = __instance.selectedWeaponSlotItem;
                    return;
                }
            }
        }

        struct ButtonInfo
        {
            public GameObject go;
            public CM_Item item;
            public Transform box;
            public TextMeshPro text;

            public readonly void SetSize(bool isDouble)
            {
                if (isDouble)
                {
                    item.SetSize(DoubleButtonSize);
                    item.m_collider.size = DoubleColliderSize;
                    box.localScale = new Vector3(DoubleSizeMod, 1f, 1f);
                }
                else
                {
                    item.SetSize(SingleButtonSize);
                    item.m_collider.size = SingleColliderSize;
                    box.localScale = Vector3.one;
                }
            }
        }

        private static ButtonInfo _leftButton;
        private static ButtonInfo _rightButton;

        [HarmonyPatch(typeof(CM_PlayerLobbyBar), nameof(CM_PlayerLobbyBar.ShowWeaponSelectionPopup))]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_LoadoutMenuOpened(CM_PlayerLobbyBar __instance)
        {
            CreateButton(__instance, ref _rightButton, ButtonPressedCallback(__instance, true));
            CreateButton(__instance, ref _leftButton, ButtonPressedCallback(__instance, false));

            // Need to manually call this since it's not called in every case we need it to be
            if (__instance.selectedWeaponSlotItem != null)
                Post_LoadoutItemSelected(__instance.selectedWeaponSlotItem);
        }

        [HarmonyPatch(typeof(CM_PlayerLobbyBar), nameof(CM_PlayerLobbyBar.OnWeaponSlotItemSelected))]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_LoadoutItemSelected(CM_InventorySlotItem slotItem)
        {
            if (_rightButton.go == null) return;

            uint id = slotItem.m_gearID.GetOfflineID();

            if (GearToggleManager.Current.TryGetToggleInfo(id, out var toggleInfo))
            {
                if (toggleInfo.text.Length > 1)
                {
                    _rightButton.go.SetActive(true);
                    _rightButton.go.transform.localPosition = DoubleButtonPos.right;
                    _rightButton.SetSize(true);
                    _rightButton.text.SetText(toggleInfo.text[0]);
                    _leftButton.go.SetActive(true);
                    _leftButton.go.transform.localPosition = DoubleButtonPos.left;
                    _leftButton.SetSize(true);
                    _leftButton.text.SetText(toggleInfo.text[1]);
                }
                else
                {
                    _rightButton.go.SetActive(true);
                    _rightButton.go.transform.localPosition = SingleButtonPos;
                    _rightButton.SetSize(false);
                    _rightButton.text.SetText(toggleInfo.text[0]);
                    _leftButton.go.SetActive(false);
                }
            }
            else
            {
                _rightButton.go.SetActive(false);
                _leftButton.go.SetActive(false);
            }
        }

        private static void CreateButton(CM_PlayerLobbyBar __instance, ref ButtonInfo button, Action<int> onPressed)
        {
            CM_ScrollWindowInfoBox infoBox = __instance.m_popupScrollWindow.InfoBox;
            // Need to instantiate a new button every time since the window is instantiated every time
            button.go = GameObject.Instantiate(CM_PageLoadout.Current.m_copyLobbyIdButton.gameObject, infoBox.transform);
            var item = button.item = button.go.GetComponent<CM_Item>();
            button.text = item.m_texts[0];
            button.box = button.go.transform.GetChild(0);

            item.transform.localPosition = new(-300, -320, -1);
            item.m_clickBlink = Configuration.ToggleBlink;
            item.OnBtnPressCallback = null;
            item.add_OnBtnPressCallback(onPressed);
        }

        private static Action<int> ButtonPressedCallback(CM_PlayerLobbyBar __instance, bool toNext)
        {
            return (id) =>
            {
                CM_InventorySlotItem? slotItem = __instance.selectedWeaponSlotItem ?? _cachedItem;
                if (slotItem == null) return;

                uint offlineID = slotItem.m_gearID.GetOfflineID();
                var toggleInfo = GearToggleManager.Current.GetToggleInfo(offlineID)!;

                List<uint> relatedIDs = toggleInfo.ids;
                int move = toNext ^ toggleInfo.reverse ? 1 : relatedIDs.Count - 1;
                uint nextID = relatedIDs[(relatedIDs.IndexOf(offlineID) + move) % relatedIDs.Count];
                if (GearManager.TryGetGear("OfflineGear_ID_" + nextID, out var newRange))
                {
                    slotItem.LoadData(newRange, true, true);
                    __instance.OnWeaponSlotItemSelected(slotItem);
                }
                else
                    DinoLogger.Error($"Couldn't swap to next weapon ({nextID}) in toggle list!");
            };
        }
    }
}
