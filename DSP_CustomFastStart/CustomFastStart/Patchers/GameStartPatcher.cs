using HarmonyLib;
using System;
using System.Collections.Generic;

namespace DSP_CustomFastStart.CustomFastStart.Patchers
{
    internal static class GameStartPatcher
    {
        private static bool _appliedForCurrentGame;
        private static bool _loadedFromSave;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameSave), "LoadCurrentGame")]
        private static void LoadCurrentGamePrefix()
        {
            _loadedFromSave = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIGalaxySelect), "_OnOpen")]
        private static void GalaxySelectOnOpenPostfix()
        {
            // Entering galaxy select indicates starting a new run flow, not loading a save.
            _loadedFromSave = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIRoot), "OnGameBegin")]
        private static void OnGameBeginPostfix()
        {
            _appliedForCurrentGame = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameLogic), "LogicFrame")]
        private static void OnLogicFramePostfix()
        {
            if (_appliedForCurrentGame || !CustomFastStartPlugin.EnableFastStart.Value)
            {
                return;
            }

            if (GameMain.instance == null || GameMain.data == null || GameMain.instance.timei != 2)
            {
                return;
            }

            if (_loadedFromSave)
            {
                _appliedForCurrentGame = true;
                CustomFastStartPlugin.Log.LogInfo("Fast start skipped because current session is loaded from save.");
                return;
            }

            _appliedForCurrentGame = true;
            ApplyFastStart();
        }

        private static void ApplyFastStart()
        {
            bool isCombat = GameMain.data.gameDesc.isCombatMode;
            string techConfig = isCombat ? CustomFastStartPlugin.TechIdsCombat.Value : CustomFastStartPlugin.TechIdsNonCombat.Value;
            string itemConfig = isCombat ? CustomFastStartPlugin.ItemsCombat.Value : CustomFastStartPlugin.ItemsNonCombat.Value;

            bool hasTechConfig = !string.IsNullOrWhiteSpace(techConfig);
            bool hasItemConfig = !string.IsNullOrWhiteSpace(itemConfig);

            if (!hasTechConfig && !hasItemConfig)
            {
                CustomFastStartPlugin.Log.LogInfo("Fast start skipped: both tech and item configs are empty.");
                return;
            }

            if (hasTechConfig)
            {
                if (!TryUnlockTechs(techConfig, out int unlockedTechCount, out string? techError))
                {
                    CustomFastStartPlugin.Log.LogWarning($"Fast start tech config parse failed: {techError}");
                }
                else
                {
                    CustomFastStartPlugin.Log.LogInfo($"Fast start unlocked techs: {unlockedTechCount}.");
                }
            }

            if (hasItemConfig)
            {
                if (!TryParseItems(itemConfig, out List<ItemGrant> items, out string? itemError))
                {
                    CustomFastStartPlugin.Log.LogWarning($"Fast start item config parse failed: {itemError}");
                    return;
                }

                if (CustomFastStartPlugin.ClearPackageBeforeGrantItems.Value)
                {
                    ClearPackage();
                }

                int granted = GrantItems(items);
                CustomFastStartPlugin.Log.LogInfo($"Fast start granted item stacks: {granted}.");
            }
        }

        private static bool TryUnlockTechs(string techConfig, out int unlockedCount, out string? error)
        {
            unlockedCount = 0;
            error = null;

            if (!TryParseTechIds(techConfig, out List<int> techIds, out error))
            {
                return false;
            }

            foreach (int techId in techIds)
            {
                if (!GameMain.data.history.TechUnlocked(techId))
                {
                    GameMain.data.history.UnlockTechUnlimited(techId, true);
                    unlockedCount++;
                }
            }

            return true;
        }

        private static bool TryParseTechIds(string rawConfig, out List<int> techIds, out string? error)
        {
            techIds = new List<int>();
            error = null;

            string[] parts = rawConfig.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0)
                {
                    continue;
                }

                if (!int.TryParse(part, out int techId))
                {
                    error = $"invalid tech id '{part}'";
                    return false;
                }

                if (LDB.techs.Select(techId) == null)
                {
                    error = $"tech id not found '{techId}'";
                    return false;
                }

                techIds.Add(techId);
            }

            if (techIds.Count == 0)
            {
                error = "no valid tech ids";
                return false;
            }

            return true;
        }

        private static bool TryParseItems(string rawConfig, out List<ItemGrant> items, out string? error)
        {
            items = new List<ItemGrant>();
            error = null;

            string[] parts = rawConfig.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0)
                {
                    continue;
                }

                string[] pair = part.Split(':');
                if (pair.Length == 0 || pair.Length > 2)
                {
                    error = $"invalid item segment '{part}'";
                    return false;
                }

                if (!int.TryParse(pair[0].Trim(), out int itemId))
                {
                    error = $"invalid item id '{pair[0]}'";
                    return false;
                }

                if (LDB.items.Select(itemId) == null)
                {
                    error = $"item id not found '{itemId}'";
                    return false;
                }

                int count = 1;
                if (pair.Length == 2 && !int.TryParse(pair[1].Trim(), out count))
                {
                    error = $"invalid item count '{pair[1]}'";
                    return false;
                }

                if (count <= 0)
                {
                    error = $"item count must be > 0 for item '{itemId}'";
                    return false;
                }

                items.Add(new ItemGrant(itemId, count));
            }

            if (items.Count == 0)
            {
                error = "no valid items";
                return false;
            }

            return true;
        }

        private static void ClearPackage()
        {
            if (GameMain.data?.mainPlayer?.package == null)
            {
                return;
            }

            StorageComponent package = GameMain.data.mainPlayer.package;
            foreach (ItemProto itemProto in LDB.items.dataArray)
            {
                if (itemProto == null)
                {
                    continue;
                }

                int itemId = itemProto.ID;
                int itemCount = package.GetItemCount(itemId);
                if (itemCount <= 0)
                {
                    continue;
                }

                int inc;
                package.TakeTailItems(ref itemId, ref itemCount, out inc);
            }
        }

        private static int GrantItems(List<ItemGrant> items)
        {
            int granted = 0;
            foreach (ItemGrant item in items)
            {
                GameMain.data.mainPlayer.TryAddItemToPackage(item.ItemId, item.Count, 0, false);
                granted++;
            }

            return granted;
        }

        private readonly struct ItemGrant
        {
            public ItemGrant(int itemId, int count)
            {
                ItemId = itemId;
                Count = count;
            }

            public int ItemId { get; }
            public int Count { get; }
        }
    }
}
