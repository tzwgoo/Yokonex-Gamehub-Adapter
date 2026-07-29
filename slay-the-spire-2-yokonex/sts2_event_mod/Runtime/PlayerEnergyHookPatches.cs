using HarmonyLib;
using System.Reflection;

namespace STS2Bridge.Runtime;

internal static class PlayerEnergyHookPatches
{
    [HarmonyPatch]
    private static class PlayerCombatStateSetEnergyPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredPlayerCombatStateMethod("set_Energy");

        [HarmonyPrefix]
        private static void Prefix(object? __instance, ref int __state)
        {
            __state = GetPreviousEnergy(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(object? __instance, int __state)
        {
            PublishEnergyChangedFromMutation("set_Energy", __instance, __state);
        }
    }

    [HarmonyPatch]
    private static class PlayerCombatStateResetEnergyPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredPlayerCombatStateMethod("ResetEnergy");

        [HarmonyPrefix]
        private static void Prefix(object? __instance, ref int __state)
        {
            __state = GetPreviousEnergy(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(object? __instance, int __state)
        {
            PublishEnergyChangedFromMutation("ResetEnergy", __instance, __state);
        }
    }

    [HarmonyPatch]
    private static class PlayerCombatStateGainEnergyPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredPlayerCombatStateMethod("GainEnergy");

        [HarmonyPrefix]
        private static void Prefix(object? __instance, ref int __state)
        {
            __state = GetPreviousEnergy(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(object? __instance, int __state)
        {
            PublishEnergyChangedFromMutation("GainEnergy", __instance, __state);
        }
    }

    [HarmonyPatch]
    private static class PlayerCombatStateLoseEnergyPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredPlayerCombatStateMethod("LoseEnergy");

        [HarmonyPrefix]
        private static void Prefix(object? __instance, ref int __state)
        {
            __state = GetPreviousEnergy(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(object? __instance, int __state)
        {
            PublishEnergyChangedFromMutation("LoseEnergy", __instance, __state);
        }
    }

    [HarmonyPatch]
    private static class PlayerCombatStateAddMaxEnergyToCurrentPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredPlayerCombatStateMethod("AddMaxEnergyToCurrent");

        [HarmonyPrefix]
        private static void Prefix(object? __instance, ref int __state)
        {
            __state = GetPreviousEnergy(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(object? __instance, int __state)
        {
            PublishEnergyChangedFromMutation("AddMaxEnergyToCurrent", __instance, __state);
        }
    }

    private static MethodBase GetRequiredPlayerCombatStateMethod(string methodName)
    {
        var playerCombatStateType = Type.GetType("MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState, sts2")
            ?? throw new InvalidOperationException("Could not locate MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState.");

        return AccessTools.Method(playerCombatStateType, methodName)
            ?? throw new InvalidOperationException($"Could not locate player combat state method '{playerCombatStateType.FullName}.{methodName}'.");
    }

    private static int GetPreviousEnergy(object? playerCombatState)
    {
        return PlayerEnergyEventBridgeLogic.TryGetEnergyValue(playerCombatState, out var energy)
            ? energy
            : ResolvePreviousEnergy(playerCombatState);
    }

    private static int ResolvePreviousEnergy(object? playerCombatState)
    {
        if (PlayerEnergyEventBridgeLogic.TryResolvePlayerId(playerCombatState, out var playerId) &&
            PlayerRuntimeStateCache.TryGetEnergy(playerId, out var energy, out _))
        {
            return energy;
        }

        return ModEntry.StateStore.GetSnapshot().Player.Energy;
    }

    private static void PublishEnergyChangedFromMutation(string source, object? playerCombatState, int previousEnergy)
    {
        var published = PlayerEnergyEventBridgeLogic.PublishEnergyChanged(
            ModEntry.EventBus,
            ModEntry.StateStore,
            playerCombatState,
            previousEnergy);

        STS2Bridge.Logging.ModLog.Info(
            $"Player energy mutation fired. source={source} stateType={playerCombatState?.GetType().FullName ?? "<null>"} previousEnergy={previousEnergy} published={published}");
    }
}
