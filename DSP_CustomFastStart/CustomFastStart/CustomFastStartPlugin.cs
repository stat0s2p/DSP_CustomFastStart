using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace DSP_CustomFastStart.CustomFastStart
{
    [BepInPlugin(ModInfo.ModGuid, ModInfo.ModName, ModInfo.Version)]
    public class CustomFastStartPlugin : BaseUnityPlugin
    {
        internal static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

        internal static ConfigEntry<bool> EnableFastStart = null!;
        internal static ConfigEntry<bool> ClearPackageBeforeGrantItems = null!;
        internal static ConfigEntry<string> TechIdsCombat = null!;
        internal static ConfigEntry<string> TechIdsNonCombat = null!;
        internal static ConfigEntry<string> ItemsCombat = null!;
        internal static ConfigEntry<string> ItemsNonCombat = null!;

        private Harmony? _harmony;

        private void Awake()
        {
            EnableFastStart = Config.Bind("General", "EnableFastStart", true, "Whether custom fast start is enabled.");
            ClearPackageBeforeGrantItems = Config.Bind("General", "ClearPackageBeforeGrantItems", true, "Whether to clear player package before adding configured items.");
            TechIdsCombat = Config.Bind("Combat", "TechIds", string.Empty, "Tech list for combat mode. Format: techId1;techId2.");
            TechIdsNonCombat = Config.Bind("NonCombat", "TechIds", string.Empty, "Tech list for non-combat mode. Format: techId1;techId2.");
            ItemsCombat = Config.Bind("Combat", "Items", "2003:300;2318:50;2316:30;2319:50;2210:20;2902:20;2202:100;2201:100;2212:30;1210:100;1131:1000;2014:200;2020:50;1804:50;2307:20;2308:20;2317:30;2310:20;2103:10;2104:10;2105:10;3008:20;3007:20;3009:20;3002:50", "Item list for combat mode. Format: itemId(:count);itemId2(:count2).");
            ItemsNonCombat = Config.Bind("NonCombat", "Items", "2003:300;2318:50;2316:30;2319:50;2210:20;2902:20;2202:100;2201:100;2212:30;1210:100;1131:1000;2014:200;2020:50;1804:50;2307:20;2308:20;2317:30;2310:20;2103:10;2104:10;2105:10", "Item list for non-combat mode. Format: itemId(:count);itemId2(:count2).");

            _harmony = new Harmony(ModInfo.ModGuid);
            _harmony.PatchAll(typeof(Patchers.GameStartPatcher));
            Log.LogInfo("Custom fast start initialized.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
