using DV.Shops;
using HarmonyLib;
using custom_item_mod;

namespace RailRider
{
    // Cans and bottles are world items only: keep them out of the shops (and out of the shop data entirely).
    [HarmonyPatch(typeof(CustomItem), nameof(CustomItem.AddToShop))]
    static class NoShopAdd { static bool Prefix(CustomItem __instance) => !PropFactory.Names.Contains(__instance.Name); }

    [HarmonyPatch(typeof(CustomItem), nameof(CustomItem.FinishInitialization))]
    static class NoShopInit { static bool Prefix(CustomItem __instance) => !PropFactory.Names.Contains(__instance.Name); }
}
