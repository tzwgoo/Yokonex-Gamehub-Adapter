using HarmonyLib;
using System.Reflection;

namespace STS2Bridge.Runtime;

internal static class RewardHookPatches
{
    [HarmonyPatch]
    private static class AfterRewardTakenPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredHookMethod("AfterRewardTaken");

        [HarmonyPostfix]
        private static void Postfix(object?[] __args)
        {
            var player = RewardEventBridgeLogic.FindPlayerArgument(__args);
            var reward = RewardEventBridgeLogic.FindRewardArgument(__args);
            RewardEventBridgeLogic.PublishRewardSelected(ModEntry.EventBus, ModEntry.StateStore, player, reward);
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
