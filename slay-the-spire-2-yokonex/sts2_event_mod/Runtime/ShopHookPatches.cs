using HarmonyLib;
using System.Reflection;

namespace STS2Bridge.Runtime;

internal static class ShopHookPatches
{
    [HarmonyPatch]
    private static class AfterItemPurchasedPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredHookMethod("AfterItemPurchased");

        [HarmonyPostfix]
        private static void Postfix(object?[] __args)
        {
            var player = ShopEventBridgeLogic.FindPlayerArgument(__args);
            var itemPurchased = ShopEventBridgeLogic.FindPurchasedItemArgument(__args);
            var goldSpent = ShopEventBridgeLogic.FindGoldSpentArgument(__args);
            ShopEventBridgeLogic.PublishItemPurchased(ModEntry.EventBus, ModEntry.StateStore, player, itemPurchased, goldSpent);
        }
    }

    private static MethodBase GetRequiredHookMethod(string methodName)
    {
        var hookType = Type.GetType("MegaCrit.Sts2.Core.Hooks.Hook, sts2")
            ?? throw new InvalidOperationException("Could not locate MegaCrit.Sts2.Core.Hooks.Hook.");

        return AccessTools.Method(hookType, methodName)
            ?? throw new InvalidOperationException($"Could not locate hook method '{hookType.FullName}.{methodName}'.");
    }
}
