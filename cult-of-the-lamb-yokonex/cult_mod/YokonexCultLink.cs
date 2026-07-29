using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Yokonex.CultOfTheLamb
{
    [BepInPlugin("net.yokonex.cult.events", "Yokonex Cult Events", "1.0.0")]
    public sealed class YokonexCultPlugin : BaseUnityPlugin
    {
        private static string logPath;
        private static readonly HashSet<int> deadEnemies = new HashSet<int>();
        private float nextPoll;
        private int followers = -1;
        private int deadFollowers = -1;
        private int tarotCards = -1;

        private void Awake()
        {
            logPath = Path.Combine(Paths.BepInExRootPath, "yokonex_events.log");
            Patch("HealthPlayer", "DealDamage", "PlayerDamagePostfix");
            Patch("HealthPlayer", "Die", "PlayerDiedPostfix");
            Patch("Health", "Heal", "HealthHealPostfix");
            Patch("Health", "DealDamage", "EnemyDamagePostfix");
            Patch("DungeonManager", "Start", "CrusadeStartedPostfix");
            Patch("DungeonManager", "OnDestroy", "CrusadeEndedPostfix");
        }

        private static void Patch(string typeName, string methodName, string postfixName)
        {
            Type type = AccessTools.TypeByName(typeName);
            MethodInfo original = type == null ? null :
                AccessTools.GetDeclaredMethods(type).FirstOrDefault(method => method.Name == methodName);
            MethodInfo postfix = AccessTools.Method(typeof(YokonexCultPlugin), postfixName);
            if (original != null && postfix != null)
                new Harmony("net.yokonex.cult.events." + typeName + "." + methodName)
                    .Patch(original, null, new HarmonyMethod(postfix));
        }

        private void Update()
        {
            if (UnityEngine.Time.unscaledTime < nextPoll) return;
            nextPoll = UnityEngine.Time.unscaledTime + 0.5f;
            object manager = StaticMember("DataManager", "Instance");
            if (manager == null) return;

            int currentFollowers = CountMember(manager, "Followers");
            int currentDead = CountMember(manager, "Followers_Dead");
            int currentTarot = CountMember(manager, "PlayerRunTrinkets");
            if (followers >= 0 && currentFollowers > followers)
                Emit("cult_of_the_lamb.follower_joined", "count", currentFollowers - followers);
            if (deadFollowers >= 0 && currentDead > deadFollowers)
                Emit("cult_of_the_lamb.follower_died", "count", currentDead - deadFollowers);
            if (deadFollowers >= 0 && currentDead < deadFollowers)
                Emit("cult_of_the_lamb.follower_revived", "count", deadFollowers - currentDead);
            if (tarotCards >= 0 && currentTarot > tarotCards)
                Emit("cult_of_the_lamb.tarot_gained", "count", currentTarot - tarotCards);
            if (tarotCards >= 0 && currentTarot < tarotCards)
                Emit("cult_of_the_lamb.tarot_lost", "count", tarotCards - currentTarot);
            followers = currentFollowers;
            deadFollowers = currentDead;
            tarotCards = currentTarot;
        }

        private static object StaticMember(string typeName, string name)
        {
            Type type = AccessTools.TypeByName(typeName);
            if (type == null) return null;
            PropertyInfo property = AccessTools.Property(type, name);
            if (property != null) return property.GetValue(null, null);
            FieldInfo field = AccessTools.Field(type, name);
            return field == null ? null : field.GetValue(null);
        }

        private static object Member(object instance, string name)
        {
            if (instance == null) return null;
            PropertyInfo property = AccessTools.Property(instance.GetType(), name);
            if (property != null) return property.GetValue(instance, null);
            FieldInfo field = AccessTools.Field(instance.GetType(), name);
            return field == null ? null : field.GetValue(instance);
        }

        private static int CountMember(object instance, string name)
        {
            ICollection collection = Member(instance, name) as ICollection;
            return collection == null ? 0 : collection.Count;
        }

        private static double NumericArg(object[] args)
        {
            if (args == null) return 0;
            foreach (object value in args)
                if (value is float || value is double || value is int)
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return 0;
        }

        private static void PlayerDamagePostfix(object[] __args)
        {
            Emit("cult_of_the_lamb.player_damaged", "amount", NumericArg(__args));
        }

        private static void PlayerDiedPostfix()
        {
            Emit("cult_of_the_lamb.player_died", "value", true);
        }

        private static void HealthHealPostfix(object __instance, object[] __args)
        {
            object team = Member(__instance, "team");
            if (__instance.GetType().Name == "HealthPlayer" ||
                (team != null && team.ToString().IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0))
                Emit("cult_of_the_lamb.player_healed", "amount", NumericArg(__args));
        }

        private static void EnemyDamagePostfix(object __instance)
        {
            object team = Member(__instance, "team");
            object hpValue = Member(__instance, "HP");
            if (team == null || hpValue == null || team.ToString().IndexOf("Team2", StringComparison.OrdinalIgnoreCase) < 0)
                return;
            if (Convert.ToDouble(hpValue, CultureInfo.InvariantCulture) > 0) return;
            int id = RuntimeHelpers.GetHashCode(__instance);
            if (deadEnemies.Add(id))
                Emit("cult_of_the_lamb.enemy_killed", "enemy", __instance.GetType().Name);
        }

        private static void CrusadeStartedPostfix()
        {
            deadEnemies.Clear();
            Emit("cult_of_the_lamb.crusade_started", "value", true);
        }

        private static void CrusadeEndedPostfix()
        {
            Emit("cult_of_the_lamb.crusade_ended", "value", true);
        }

        private static string Json(object value)
        {
            if (value == null) return "null";
            if (value is bool) return (bool)value ? "true" : "false";
            if (value is string)
                return "\"" + ((string)value).Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static void Emit(string eventKey, string name, object value)
        {
            try
            {
                // 每行一个 JSON，桥接器可在游戏崩溃后安全恢复监听。
                File.AppendAllText(logPath,
                    "{\"eventKey\":" + Json(eventKey) + ",\"data\":{\"" + name + "\":" + Json(value) + "}}\n");
            }
            catch { }
        }
    }
}
