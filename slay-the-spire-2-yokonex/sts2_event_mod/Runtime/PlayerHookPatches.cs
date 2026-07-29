using HarmonyLib;
using System.Reflection;
using STS2Bridge.Logging;

namespace STS2Bridge.Runtime;

internal static class PlayerHookPatches
{
    private const string HookTypeName = "MegaCrit.Sts2.Core.Hooks.Hook, sts2";

    [HarmonyPatch]
    private static class AfterCurrentHpChangedPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredHookMethod("AfterCurrentHpChanged");

        [HarmonyPostfix]
        private static void Postfix(object?[] __args)
        {
            if (__args is null || __args.Length < 4)
            {
                ModLog.Warn($"AfterCurrentHpChanged skipped because args were missing. args={DescribeArgs(__args)}");
                return;
            }

            if (!RuntimeReflectionHelpers.TryConvertToInt(__args[3], out var delta))
            {
                ModLog.Warn($"AfterCurrentHpChanged skipped because delta could not be converted. args={DescribeArgs(__args)}");
                return;
            }

            var published = PlayerEventBridgeLogic.PublishHpChanged(ModEntry.EventBus, ModEntry.StateStore, __args[2], delta);
            ModLog.Info($"AfterCurrentHpChanged fired. creatureType={__args[2]?.GetType().FullName ?? "<null>"} delta={delta} published={published}");
        }
    }

    [HarmonyPatch]
    private static class AfterBlockGainedPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredHookMethod("AfterBlockGained");

        [HarmonyPostfix]
        private static void Postfix(object?[] __args)
        {
            if (__args is null || __args.Length < 3)
            {
                ModLog.Warn($"AfterBlockGained skipped because args were missing. args={DescribeArgs(__args)}");
                return;
            }

            if (!RuntimeReflectionHelpers.TryConvertToInt(__args[2], out var delta))
            {
                ModLog.Warn($"AfterBlockGained skipped because amount could not be converted. args={DescribeArgs(__args)}");
                return;
            }

            var published = PlayerEventBridgeLogic.PublishBlockChanged(ModEntry.EventBus, ModEntry.StateStore, __args[1], delta, "gained");
            ModLog.Info($"AfterBlockGained fired. creatureType={__args[1]?.GetType().FullName ?? "<null>"} delta={delta} published={published}");
        }
    }

    [HarmonyPatch]
    private static class AfterBlockClearedPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredHookMethod("AfterBlockCleared");

        [HarmonyPostfix]
        private static void Postfix(object?[] __args)
        {
            var creature = FindPlayerCreatureArgument(__args);
            if (creature is null)
            {
                ModLog.Warn($"AfterBlockCleared skipped because no player creature was found. args={DescribeArgs(__args)}");
                return;
            }

            var refreshed = PlayerEventBridgeLogic.RefreshBlockCleared(ModEntry.StateStore, creature);
            ModLog.Info($"AfterBlockCleared fired. creatureType={creature.GetType().FullName} refreshed={refreshed}");
        }
    }

    [HarmonyPatch]
    private static class AfterBlockBrokenPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredHookMethod("AfterBlockBroken");

        [HarmonyPostfix]
        private static void Postfix(object?[] __args)
        {
            var creature = FindPlayerCreatureArgument(__args);
            if (creature is null)
            {
                ModLog.Warn($"AfterBlockBroken skipped because no player creature was found. args={DescribeArgs(__args)}");
                return;
            }

            var previousBlock = PlayerBlockTransitionCache.TryConsume(creature, out var cachedPreviousBlock)
                ? cachedPreviousBlock
                : ResolvePreviousBlock(creature);
            var published = PlayerEventBridgeLogic.PublishBlockBrokenFromTransition(ModEntry.EventBus, ModEntry.StateStore, creature, previousBlock);
            ModLog.Info($"AfterBlockBroken fired. creatureType={creature.GetType().FullName} previousBlock={previousBlock} published={published}");
        }
    }

    [HarmonyPatch]
    private static class AfterDamageReceivedPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredHookMethod("AfterDamageReceived");

        [HarmonyPostfix]
        private static void Postfix(object?[] __args)
        {
            var creature = PlayerHookArgumentLogic.FindDamageTargetPlayerArgument(__args);
            if (creature is null)
            {
                ModLog.Warn($"AfterDamageReceived skipped because no player creature was found. args={DescribeArgs(__args)}");
                return;
            }

            var result = FindDamageResultArgument(__args);
            var published = PlayerEventBridgeLogic.PublishDamageReceived(ModEntry.EventBus, ModEntry.StateStore, creature, result);
            ModLog.Info($"AfterDamageReceived fired. creatureType={creature.GetType().FullName} resultType={result?.GetType().FullName ?? "<null>"} published={published}");
        }
    }

    [HarmonyPatch]
    private static class AfterDeathPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredHookMethod("AfterDeath");

        [HarmonyPostfix]
        private static void Postfix(object?[] __args)
        {
            var creature = FindPlayerCreatureArgument(__args);
            if (creature is null)
            {
                return;
            }

            var wasRemovalPrevented = false;
            if (__args is not null)
            {
                foreach (var arg in __args)
                {
                    if (arg is bool flag)
                    {
                        wasRemovalPrevented = flag;
                        break;
                    }
                }
            }

            PlayerEventBridgeLogic.PublishPlayerDied(ModEntry.EventBus, ModEntry.StateStore, creature, wasRemovalPrevented);
        }
    }

    [HarmonyPatch]
    private static class CreatureLoseBlockInternalPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredCreatureMethod(PlayerBlockHookTargetCatalog.BlockLossMethodNames[0]);

        [HarmonyPrefix]
        private static void Prefix(object? __instance, ref int __state)
        {
            __state = PlayerEventBridgeLogic.TryGetBlockValue(__instance, out var block) ? block : -1;
            PlayerBlockTransitionCache.Store(__instance, __state);
            ModLog.Info($"LoseBlockInternal prefix. creatureType={__instance?.GetType().FullName ?? "<null>"} previousBlock={__state}");
        }

        [HarmonyPostfix]
        private static void Postfix(object? __instance, int __state)
        {
            if (__state < 0)
            {
                ModLog.Warn($"LoseBlockInternal postfix skipped because previous block was unavailable. creatureType={__instance?.GetType().FullName ?? "<null>"}");
                return;
            }

            var published = PlayerEventBridgeLogic.PublishBlockLossFromTransition(ModEntry.EventBus, ModEntry.StateStore, __instance, __state, "lost");
            ModLog.Info($"LoseBlockInternal postfix fired. creatureType={__instance?.GetType().FullName ?? "<null>"} previousBlock={__state} published={published}");
        }
    }

    [HarmonyPatch]
    private static class CreatureDamageBlockInternalPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredCreatureMethod(PlayerBlockHookTargetCatalog.BlockLossMethodNames[1]);

        [HarmonyPrefix]
        private static void Prefix(object? __instance, ref int __state)
        {
            __state = PlayerEventBridgeLogic.TryGetBlockValue(__instance, out var block) ? block : -1;
            PlayerBlockTransitionCache.Store(__instance, __state);
            ModLog.Info($"DamageBlockInternal prefix. creatureType={__instance?.GetType().FullName ?? "<null>"} previousBlock={__state}");
        }

        [HarmonyPostfix]
        private static void Postfix(object? __instance, int __state)
        {
            if (__state < 0)
            {
                ModLog.Warn($"DamageBlockInternal postfix skipped because previous block was unavailable. creatureType={__instance?.GetType().FullName ?? "<null>"}");
                return;
            }

            var published = PlayerEventBridgeLogic.PublishBlockLossFromTransition(ModEntry.EventBus, ModEntry.StateStore, __instance, __state, "damaged");
            ModLog.Info($"DamageBlockInternal postfix fired. creatureType={__instance?.GetType().FullName ?? "<null>"} previousBlock={__state} published={published}");
        }
    }

    private static MethodBase GetRequiredHookMethod(string methodName)
    {
        var hookType = Type.GetType(HookTypeName)
            ?? throw new InvalidOperationException($"Could not locate hook type '{HookTypeName}'.");

        return AccessTools.Method(hookType, methodName)
            ?? throw new InvalidOperationException($"Could not locate hook method '{hookType.FullName}.{methodName}'.");
    }

    private static MethodBase GetRequiredCreatureMethod(string methodName)
    {
        var creatureType = Type.GetType("MegaCrit.Sts2.Core.Entities.Creatures.Creature, sts2")
            ?? throw new InvalidOperationException("Could not locate MegaCrit.Sts2.Core.Entities.Creatures.Creature.");

        return AccessTools.Method(creatureType, methodName)
            ?? throw new InvalidOperationException($"Could not locate creature method '{creatureType.FullName}.{methodName}'.");
    }

    private static object? FindPlayerCreatureArgument(object?[]? args)
    {
        if (args is null)
        {
            return null;
        }

        foreach (var arg in args)
        {
            if (PlayerEventBridgeLogic.IsPlayerCreature(arg))
            {
                return arg;
            }
        }

        return null;
    }

    private static object? FindDamageResultArgument(object?[]? args)
    {
        if (args is null)
        {
            return null;
        }

        foreach (var arg in args)
        {
            if (arg is null)
            {
                continue;
            }

            if (RuntimeReflectionHelpers.TryGetInt(arg, ["BlockedDamage", "blockedDamage"], out _) &&
                RuntimeReflectionHelpers.TryGetInt(arg, ["UnblockedDamage", "unblockedDamage"], out _))
            {
                return arg;
            }
        }

        return null;
    }

    private static int ResolvePreviousBlock(object creature)
    {
        if (PlayerEventBridgeLogic.TryResolvePlayerId(creature, out var playerId) &&
            PlayerRuntimeStateCache.TryGetBlock(playerId, out var previousBlock))
        {
            return previousBlock;
        }

        return ModEntry.StateStore.GetSnapshot().Player.Block;
    }

    private static string DescribeArgs(object?[]? args)
    {
        if (args is null || args.Length == 0)
        {
            return "[]";
        }

        return "[" + string.Join(", ", args.Select((arg, index) => $"{index}:{arg?.GetType().FullName ?? "<null>"}")) + "]";
    }
}
