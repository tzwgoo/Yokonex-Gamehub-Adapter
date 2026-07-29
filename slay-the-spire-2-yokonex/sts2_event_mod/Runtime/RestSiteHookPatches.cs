using HarmonyLib;
using System.Reflection;

namespace STS2Bridge.Runtime;

internal static class RestSiteHookPatches
{
    [HarmonyPatch]
    private static class AfterRestSiteSmithPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredHookMethod("AfterRestSiteSmith");

        [HarmonyPostfix]
        private static void Postfix(object?[] __args)
        {
            var runState = RestSiteEventBridgeLogic.FindRunStateArgument(__args);
            var player = RestSiteEventBridgeLogic.FindPlayerArgument(__args);
            RestSiteEventBridgeLogic.PublishCardUpgraded(ModEntry.EventBus, ModEntry.StateStore, runState, player);
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
