using System.Runtime.CompilerServices;

namespace STS2Bridge.Runtime;

internal static class PlayerDamageTransitionCache
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<int, DamageMarker> MarkerByCreatureKey = new();

    public static void Store(object? creature, int currentHp, int amount)
    {
        if (creature is null || amount <= 0)
        {
            return;
        }

        lock (SyncRoot)
        {
            MarkerByCreatureKey[RuntimeHelpers.GetHashCode(creature)] = new DamageMarker(currentHp, amount);
        }
    }

    public static bool TryConsume(object? creature, int currentHp, int amount)
    {
        if (creature is null || amount <= 0)
        {
            return false;
        }

        lock (SyncRoot)
        {
            var key = RuntimeHelpers.GetHashCode(creature);
            if (!MarkerByCreatureKey.TryGetValue(key, out var marker))
            {
                return false;
            }

            if (marker.CurrentHp != currentHp || marker.Amount != amount)
            {
                return false;
            }

            MarkerByCreatureKey.Remove(key);
            return true;
        }
    }

    public static void Clear()
    {
        lock (SyncRoot)
        {
            MarkerByCreatureKey.Clear();
        }
    }

    private readonly record struct DamageMarker(int CurrentHp, int Amount);
}
