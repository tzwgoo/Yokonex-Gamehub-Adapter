using HarmonyLib;
using System.Reflection;

namespace STS2Bridge.Runtime;

internal static class CardUpgradeHookPatches
{
    [HarmonyPatch]
    private static class AfterForgePatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredHookMethod("AfterForge");

        [HarmonyPostfix]
        private static void Postfix(object?[] __args)
        {
            var player = RestSiteEventBridgeLogic.FindPlayerArgument(__args);
            var card = RestSiteEventBridgeLogic.FindCardArgument(__args);
            var upgradeAmount = FindUpgradeAmount(__args);
            RestSiteEventBridgeLogic.PublishCardUpgradedFromSource(
                ModEntry.EventBus,
                ModEntry.StateStore,
                player,
                card,
                "forge",
                upgradeAmount);
        }
    }

    private static int FindUpgradeAmount(object?[]? args)
    {
        if (args is null)
        {
            return 1;
        }

        foreach (var arg in args)
        {
            if (arg is int value && value > 0)
            {
                return value;
            }
        }

        return 1;
    }

    private static MethodBase GetRequiredHookMethod(string methodName)
    {
        var hookType = Type.GetType("MegaCrit.Sts2.Core.Hooks.Hook, sts2")
            ?? throw new InvalidOperationException("Could not locate MegaCrit.Sts2.Core.Hooks.Hook.");

        return AccessTools.Method(hookType, methodName)
            ?? throw new InvalidOperationException($"Could not locate hook method '{hookType.FullName}.{methodName}'.");
    }
}
