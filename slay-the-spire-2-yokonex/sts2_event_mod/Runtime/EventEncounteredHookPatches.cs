using HarmonyLib;
using System.Reflection;

namespace STS2Bridge.Runtime;

internal static class EventEncounteredHookPatches
{
    [HarmonyPatch]
    private static class EventRoomEnterInternalPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            var eventRoomType = Type.GetType("MegaCrit.Sts2.Core.Rooms.EventRoom, sts2")
                ?? throw new InvalidOperationException("Could not locate MegaCrit.Sts2.Core.Rooms.EventRoom.");

            return AccessTools.Method(eventRoomType, "EnterInternal")
                ?? throw new InvalidOperationException($"Could not locate event room method '{eventRoomType.FullName}.EnterInternal'.");
        }

        [HarmonyPrefix]
        private static void Prefix(object? __instance, object?[] __args)
        {
            var isRestoringRoomStackBase = EventEncounteredEventBridgeLogic.FindIsRestoringRoomStackBaseArgument(__args);
            EventEncounteredEventBridgeLogic.PublishEventEncountered(
                ModEntry.EventBus,
                ModEntry.StateStore,
                __instance,
                isRestoringRoomStackBase);
        }
    }
}
