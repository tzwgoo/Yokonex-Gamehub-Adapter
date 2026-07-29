using System.Runtime.CompilerServices;

namespace STS2Bridge.Runtime;

internal static class PlayerBlockTransitionCache
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<int, int> PreviousBlockByCreatureKey = new();

    public static void Store(object? creature, int previousBlock)
    {
        if (creature is null || previousBlock < 0)
        {
            return;
        }

        lock (SyncRoot)
        {
            PreviousBlockByCreatureKey[RuntimeHelpers.GetHashCode(creature)] = previousBlock;
        }
    }

    public static bool TryConsume(object? creature, out int previousBlock)
    {
        previousBlock = default;
        if (creature is null)
        {
            return false;
        }

        lock (SyncRoot)
        {
            var key = RuntimeHelpers.GetHashCode(creature);
            if (!PreviousBlockByCreatureKey.TryGetValue(key, out previousBlock))
            {
                return false;
            }

            PreviousBlockByCreatureKey.Remove(key);
            return true;
        }
    }

    public static void Clear()
    {
        lock (SyncRoot)
        {
            PreviousBlockByCreatureKey.Clear();
        }
    }
}
