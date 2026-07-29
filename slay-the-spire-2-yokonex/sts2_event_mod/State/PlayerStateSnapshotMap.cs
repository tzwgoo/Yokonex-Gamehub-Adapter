using STS2Bridge.State.Dtos;

namespace STS2Bridge.State;

internal static class PlayerStateSnapshotMap
{
    public static IReadOnlyDictionary<string, PlayerStateDto> Upsert(
        IReadOnlyDictionary<string, PlayerStateDto> playersById,
        string playerId,
        Func<PlayerStateDto, PlayerStateDto> update)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        ArgumentNullException.ThrowIfNull(update);

        var current = playersById.TryGetValue(playerId, out var existing)
            ? existing
            : new PlayerStateDto(0, 0, 0, 0, 0);

        var next = new Dictionary<string, PlayerStateDto>(playersById, StringComparer.Ordinal)
        {
            [playerId] = update(current)
        };

        return next;
    }
}
