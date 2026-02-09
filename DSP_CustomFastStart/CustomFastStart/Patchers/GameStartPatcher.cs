using HarmonyLib;
using System;
using System.Collections.Generic;

namespace DSP_CustomFastStart.CustomFastStart.Patchers
{
    public static class GameStartPatcher
    {
        private enum FastStartContext
        {
            Unknown = 0,
            NewGameRequested = 1,
            LoadedSave = 2
        }

        private static bool _appliedForCurrentGame;
        private static bool _galaxySelectOpened;
        private static bool _startupStateLogged;
        private static bool _pendingNewGameFlow;
        private static FastStartContext _context = FastStartContext.Unknown;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameSave), "LoadCurrentGame")]
        public static void LoadCurrentGamePrefix()
        {
            // DSP can call LoadCurrentGame for both true save loads and newly-created games.
            // If we just left galaxy select for a new run, keep NewGameRequested context.
            if (_pendingNewGameFlow)
            {
                CustomFastStartPlugin.Log.LogInfo("FastStart mark: LoadCurrentGame observed during pending new-game flow; keeping NewGameRequested context.");
                return;
            }

            _context = FastStartContext.LoadedSave;
            CustomFastStartPlugin.Log.LogInfo("FastStart mark: session entered via LoadCurrentGame (treated as save load).");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIGalaxySelect), "_OnOpen")]
        public static void GalaxySelectOnOpenPostfix()
        {
            _galaxySelectOpened = true;
            _pendingNewGameFlow = false;
            _context = FastStartContext.Unknown;
            CustomFastStartPlugin.Log.LogInfo("FastStart mark: galaxy select opened (new game flow).");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIGalaxySelect), "_OnClose")]
        public static void GalaxySelectOnClosePostfix()
        {
            if (_galaxySelectOpened)
            {
                // Closing galaxy select in the start flow is a reliable indicator that
                // we are moving forward to create a new game (mirrors DSP_Battle's timing intent).
                _context = FastStartContext.NewGameRequested;
                _pendingNewGameFlow = true;
                _galaxySelectOpened = false;
                CustomFastStartPlugin.Log.LogInfo("FastStart mark: galaxy select closed, new game requested.");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIRoot), "OnGameBegin")]
        public static void OnGameBeginPostfix()
        {
            _appliedForCurrentGame = false;
            _startupStateLogged = false;
            CustomFastStartPlugin.Log.LogInfo("FastStart reset: OnGameBegin.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameLogic), "LogicFrame")]
        public static void OnLogicFramePostfix()
        {
            if (_appliedForCurrentGame || !CustomFastStartPlugin.EnableFastStart.Value)
            {
                return;
            }

            if (GameMain.instance == null || GameMain.data == null)
            {
                return;
            }

            // timei == 2 can be missed depending on load order and frame timing.
            // Execute on the first frame after startup when core objects are ready.
            if (GameMain.instance.timei < 2 || GameMain.data.mainPlayer == null)
            {
                return;
            }

            if (!_startupStateLogged)
            {
                _startupStateLogged = true;
                CustomFastStartPlugin.Log.LogInfo($"FastStart check: timei={GameMain.instance.timei}, context={_context}.");
            }

            if (_context != FastStartContext.NewGameRequested)
            {
                _appliedForCurrentGame = true;
                _pendingNewGameFlow = false;
                CustomFastStartPlugin.Log.LogInfo($"Fast start skipped because context is {_context}, not NewGameRequested.");
                return;
            }

            try
            {
                _appliedForCurrentGame = true;
                _pendingNewGameFlow = false;
                ApplyFastStart();
            }
            catch (Exception ex)
            {
                _appliedForCurrentGame = true;
                _pendingNewGameFlow = false;
                CustomFastStartPlugin.Log.LogError($"Fast start apply failed: {ex}");
            }
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

            CustomFastStartPlugin.Log.LogInfo($"Fast start apply begin. isCombat={isCombat}, hasTechConfig={hasTechConfig}, hasItemConfig={hasItemConfig}.");

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
