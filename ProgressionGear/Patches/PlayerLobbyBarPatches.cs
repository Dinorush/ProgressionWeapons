using CellMenu;
using Gear;
using HarmonyLib;
using Player;
using ProgressionGear.Dependencies;
using ProgressionGear.GearToggle;
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
        private static readonly Vector3 SingleButtonPos = new(360, -800, -2);
        private static readonly Vector3 DoubleButtonPos = new(500, -800, -2);
        private static readonly Vector3 SingleColliderOffset = new(90, -6.5f);
        private static readonly Vector3 DoubleColliderOffset = new(100, -6.5f);

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
            public CM_ScrollWindowHeader item;
            public BoxCollider2D collider;
            public TextMeshPro text;

            public readonly void SetMode(bool isDouble)
            {
                if (isDouble)
                {
                    go.transform.localPosition = DoubleButtonPos;
                    collider.offset = DoubleColliderOffset;
                }
                else
                {
                    go.transform.localPosition = SingleButtonPos;
                    collider.offset = SingleColliderOffset;
                }
            }
        }

        private static ButtonInfo _leftButton;
        private static ButtonInfo _rightButton;
        private static CM_PlayerLobbyBar? _activeBar;

        [HarmonyPatch(typeof(CM_ScrollWindow), nameof(CM_ScrollWindow.Setup), new Type[] { })]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_LobbyBarSetup(CM_ScrollWindow __instance)
        {
            if (_rightButton.go != null) return;

            CreateButton(__instance, ref _rightButton, true);
            CreateButton(__instance, ref _leftButton, false);
        }

        [HarmonyPatch(typeof(CM_PlayerLobbyBar), nameof(CM_PlayerLobbyBar.ShowWeaponSelectionPopup))]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_LoadoutMenuOpened(CM_PlayerLobbyBar __instance)
        {
            var window = __instance.m_popupScrollWindow;
            _rightButton.go.transform.SetParent(window.transform, false);
            _leftButton.go.transform.SetParent(window.transform, false);
            _rightButton.go.transform.localPosition = DoubleButtonPos;
            _leftButton.go.transform.localPosition = DoubleButtonPos;
            window.AddNonContentItem(_rightButton.item);
            window.AddNonContentItem(_leftButton.item);

            _activeBar = __instance;
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
                    _rightButton.SetMode(true);
                    _rightButton.text.SetText(toggleInfo.text[0]);
                    _leftButton.go.SetActive(true);
                    _leftButton.text.SetText(toggleInfo.text[1]);
                }
                else
                {
                    _rightButton.go.SetActive(true);
                    _rightButton.SetMode(false);
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

        private static void CreateButton(CM_ScrollWindow window, ref ButtonInfo button, bool right)
        {
            button.go = GameObject.Instantiate(window.m_headers[0].gameObject);
            button.go.name = "PG_" + (right ? "Right" : "Left");
            var item = button.item = button.go.GetComponent<CM_ScrollWindowHeader>();
            item.SetSelected(false);

            var transform = button.go.transform;
            transform.localScale = Vector3.one * 1.5f;
            transform.Rotate(0, right ? 180 : 0, 180);

            var bg = transform.GetChild(0);
            bg.localPosition = new(5, -25, 0);
            bg.GetComponent<SpriteRenderer>().size = new(2600, 29);

            var text = transform.GetChild(1);
            text.Rotate(0, right ? 180 : 0, 180);
            text.localPosition = right ? new(-10, -5, 0) : new(185, -5, 0);
            button.text = item.m_texts[0];
            button.text.alignment = TextAlignmentOptions.Midline;

            var headline = transform.GetChild(2);
            headline.localScale = new(0.8f, 1f, 1f);

            button.collider = item.m_collider;
            button.collider.size = new(200, 40f);
            button.collider.offset = DoubleColliderOffset;

            item.OnBtnPressCallback = null;
            item.add_OnBtnPressCallback(ButtonPressedCallback(right));
        }

        private static Action<int> ButtonPressedCallback(bool toNext)
        {
            return (id) =>
            {
                CM_InventorySlotItem? slotItem = _activeBar!.selectedWeaponSlotItem ?? _cachedItem;
                if (slotItem == null) return;

                uint offlineID = slotItem.m_gearID.GetOfflineID();
                var toggleInfo = GearToggleManager.Current.GetToggleInfo(offlineID)!;

                List<uint> relatedIDs = toggleInfo.ids;
                int move = toNext ^ toggleInfo.reverse ? 1 : relatedIDs.Count - 1;
                uint nextID = relatedIDs[(relatedIDs.IndexOf(offlineID) + move) % relatedIDs.Count];
                if (GearManager.TryGetGear("OfflineGear_ID_" + nextID, out var newRange))
                {
                    slotItem.LoadData(newRange, true, true);
                    _activeBar.OnWeaponSlotItemSelected(slotItem);
                }
                else
                    DinoLogger.Error($"Couldn't swap to next weapon ({nextID}) in toggle list!");
            };
        }
    }
}
