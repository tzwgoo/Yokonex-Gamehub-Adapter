using HarmonyLib;
using STS2Bridge.Logging;
using System.Reflection;

namespace STS2Bridge.Runtime;

internal static class OrbHookPatches
{
    [HarmonyPatch]
    private static class LightningPassivePatch
    {
        [HarmonyPrepare]
        private static bool Prepare() => HasMethod("MegaCrit.Sts2.Core.Models.Orbs.LightningOrb, sts2", "Passive");

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredMethod("MegaCrit.Sts2.Core.Models.Orbs.LightningOrb, sts2", "Passive");

        [HarmonyPostfix]
        private static void Postfix(object? __instance) => PublishPassive(__instance, "lightning", "damage");
    }

    [HarmonyPatch]
    private static class LightningEvokePatch
    {
        [HarmonyPrepare]
        private static bool Prepare() => HasMethod("MegaCrit.Sts2.Core.Models.Orbs.LightningOrb, sts2", "Evoke");

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredMethod("MegaCrit.Sts2.Core.Models.Orbs.LightningOrb, sts2", "Evoke");

        [HarmonyPostfix]
        private static void Postfix(object? __instance) => PublishEvoke(__instance, "lightning", "damage");
    }

    [HarmonyPatch]
    private static class FrostPassivePatch
    {
        [HarmonyPrepare]
        private static bool Prepare() => HasMethod("MegaCrit.Sts2.Core.Models.Orbs.FrostOrb, sts2", "Passive");

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredMethod("MegaCrit.Sts2.Core.Models.Orbs.FrostOrb, sts2", "Passive");

        [HarmonyPostfix]
        private static void Postfix(object? __instance) => PublishPassive(__instance, "frost", "block");
    }

    [HarmonyPatch]
    private static class FrostEvokePatch
    {
        [HarmonyPrepare]
        private static bool Prepare() => HasMethod("MegaCrit.Sts2.Core.Models.Orbs.FrostOrb, sts2", "Evoke");

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredMethod("MegaCrit.Sts2.Core.Models.Orbs.FrostOrb, sts2", "Evoke");

        [HarmonyPostfix]
        private static void Postfix(object? __instance) => PublishEvoke(__instance, "frost", "block");
    }

    [HarmonyPatch]
    private static class DarkPassivePatch
    {
        [HarmonyPrepare]
        private static bool Prepare() => HasMethod("MegaCrit.Sts2.Core.Models.Orbs.DarkOrb, sts2", "Passive");

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredMethod("MegaCrit.Sts2.Core.Models.Orbs.DarkOrb, sts2", "Passive");

        [HarmonyPostfix]
        private static void Postfix(object? __instance) => PublishPassive(__instance, "dark", "damage");
    }

    [HarmonyPatch]
    private static class DarkEvokePatch
    {
        [HarmonyPrepare]
        private static bool Prepare() => HasMethod("MegaCrit.Sts2.Core.Models.Orbs.DarkOrb, sts2", "Evoke");

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredMethod("MegaCrit.Sts2.Core.Models.Orbs.DarkOrb, sts2", "Evoke");

        [HarmonyPostfix]
        private static void Postfix(object? __instance) => PublishEvoke(__instance, "dark", "damage");
    }

    [HarmonyPatch]
    private static class PlasmaPassivePatch
    {
        [HarmonyPrepare]
        private static bool Prepare() => HasMethod("MegaCrit.Sts2.Core.Models.Orbs.PlasmaOrb, sts2", "Passive");

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredMethod("MegaCrit.Sts2.Core.Models.Orbs.PlasmaOrb, sts2", "Passive");

        [HarmonyPostfix]
        private static void Postfix(object? __instance) => PublishPassive(__instance, "plasma", "energy");
    }

    [HarmonyPatch]
    private static class PlasmaEvokePatch
    {
        [HarmonyPrepare]
        private static bool Prepare() => HasMethod("MegaCrit.Sts2.Core.Models.Orbs.PlasmaOrb, sts2", "Evoke");

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredMethod("MegaCrit.Sts2.Core.Models.Orbs.PlasmaOrb, sts2", "Evoke");

        [HarmonyPostfix]
        private static void Postfix(object? __instance) => PublishEvoke(__instance, "plasma", "energy");
    }

    [HarmonyPatch]
    private static class GlassPassivePatch
    {
        [HarmonyPrepare]
        private static bool Prepare() => HasMethod("MegaCrit.Sts2.Core.Models.Orbs.GlassOrb, sts2", "Passive");

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredMethod("MegaCrit.Sts2.Core.Models.Orbs.GlassOrb, sts2", "Passive");

        [HarmonyPostfix]
        private static void Postfix(object? __instance) => PublishPassive(__instance, "glass", "damage");
    }

    [HarmonyPatch]
    private static class GlassEvokePatch
    {
        [HarmonyPrepare]
        private static bool Prepare() => HasMethod("MegaCrit.Sts2.Core.Models.Orbs.GlassOrb, sts2", "Evoke");

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => GetRequiredMethod("MegaCrit.Sts2.Core.Models.Orbs.GlassOrb, sts2", "Evoke");

        [HarmonyPostfix]
        private static void Postfix(object? __instance) => PublishEvoke(__instance, "glass", "damage");
    }

    private static void PublishPassive(object? orb, string orbType, string amountKind)
    {
        var published = OrbEventBridgeLogic.PublishPassiveTriggered(ModEntry.EventBus, ModEntry.StateStore, orb, orbType, amountKind);
        ModLog.Info($"Orb passive fired. orbType={orbType} amountKind={amountKind} runtimeType={orb?.GetType().FullName ?? "<null>"} published={published}");
    }

    private static void PublishEvoke(object? orb, string orbType, string amountKind)
    {
        var published = OrbEventBridgeLogic.PublishEvoked(ModEntry.EventBus, ModEntry.StateStore, orb, orbType, amountKind);
        ModLog.Info($"Orb evoke fired. orbType={orbType} amountKind={amountKind} runtimeType={orb?.GetType().FullName ?? "<null>"} published={published}");
    }

    private static bool HasMethod(string typeName, string methodName)
    {
        var type = Type.GetType(typeName);
        return type is not null && AccessTools.Method(type, methodName) is not null;
    }

    private static MethodBase GetRequiredMethod(string typeName, string methodName)
    {
        var type = Type.GetType(typeName)
            ?? throw new InvalidOperationException($"Could not locate orb type '{typeName}'.");

        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Could not locate orb method '{type.FullName}.{methodName}'.");
    }
}
