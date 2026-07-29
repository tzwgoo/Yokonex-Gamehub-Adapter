namespace STS2Bridge.Runtime;

internal static class PlayerRuntimeStateCache
{
    private static readonly Lock SyncRoot = new();
    private static readonly Dictionary<string, PlayerRuntimeState> StateByPlayerId = new(StringComparer.Ordinal);

    public static void StoreEnergy(string playerId, int energy, int maxEnergy)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        lock (SyncRoot)
        {
            var existing = StateByPlayerId.TryGetValue(playerId, out var current)
                ? current
                : default;

            StateByPlayerId[playerId] = existing with
            {
                Energy = energy,
                MaxEnergy = maxEnergy,
                HasEnergy = true
            };
        }
    }

    public static bool TryGetEnergy(string playerId, out int energy, out int maxEnergy)
    {
        energy = default;
        maxEnergy = default;

        if (string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        lock (SyncRoot)
        {
            if (!StateByPlayerId.TryGetValue(playerId, out var state) || !state.HasEnergy)
            {
                return false;
            }

            energy = state.Energy;
            maxEnergy = state.MaxEnergy;
            return true;
        }
    }

    public static void StoreBlock(string playerId, int block)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        lock (SyncRoot)
        {
            var existing = StateByPlayerId.TryGetValue(playerId, out var current)
                ? current
                : default;

            StateByPlayerId[playerId] = existing with
            {
                Block = block,
                HasBlock = true
            };
        }
    }

    public static bool TryGetBlock(string playerId, out int block)
    {
        block = default;

        if (string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        lock (SyncRoot)
        {
            if (!StateByPlayerId.TryGetValue(playerId, out var state) || !state.HasBlock)
            {
                return false;
            }

            block = state.Block;
            return true;
        }
    }

    public static void Clear()
    {
        lock (SyncRoot)
        {
            StateByPlayerId.Clear();
        }
    }

    private readonly record struct PlayerRuntimeState(
        int Energy,
        int MaxEnergy,
        bool HasEnergy,
        int Block,
        bool HasBlock);
}
