using HarmonyLib;
using System.Reflection;

namespace STS2Bridge.Runtime;

internal static class RunAbandonedHookPatches
{
    [HarmonyPatch]
    private static class RunManagerAbandonPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredRunManagerMethod("Abandon");

        [HarmonyPrefix]
        private static void Prefix(object? __instance)
        {
            RunAbandonedEventBridgeLogic.PublishRunAbandoned(
                ModEntry.EventBus,
                ModEntry.StateStore,
                __instance);
        }
    }

    private static MethodBase GetRequiredRunManagerMethod(string methodName)
    {
        var runManagerType = Type.GetType("MegaCrit.Sts2.Core.Runs.RunManager, sts2")
            ?? throw new InvalidOperationException("Could not locate MegaCrit.Sts2.Core.Runs.RunManager.");

        return AccessTools.Method(runManagerType, methodName)
            ?? throw new InvalidOperationException($"Could not locate run manager method '{runManagerType.FullName}.{methodName}'.");
    }
}
