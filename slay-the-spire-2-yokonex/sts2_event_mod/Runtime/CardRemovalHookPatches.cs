using HarmonyLib;
using System.Reflection;

namespace STS2Bridge.Runtime;

internal static class CardRemovalHookPatches
{
    [HarmonyPatch]
    private static class BeforeCardRemovedPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredHookMethod("BeforeCardRemoved");

        [HarmonyPrefix]
        private static void Prefix(object?[] __args)
        {
            var player = CardRemovalEventBridgeLogic.FindPlayerArgument(__args);
            var card = CardRemovalEventBridgeLogic.FindCardArgument(__args);
            var source = CardRemovalEventBridgeLogic.InferSource(__args);
            var goldSpent = CardRemovalEventBridgeLogic.FindGoldSpentArgument(__args);
            CardRemovalEventBridgeLogic.PublishCardRemoved(
                ModEntry.EventBus,
                ModEntry.StateStore,
                player,
                card,
                source,
                goldSpent);
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
