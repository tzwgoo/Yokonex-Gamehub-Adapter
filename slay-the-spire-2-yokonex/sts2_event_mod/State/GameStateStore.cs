using STS2Bridge.State.Dtos;

namespace STS2Bridge.State;

public sealed class GameStateStore
{
    private readonly Lock _lock = new();
    private StateSnapshotDto _snapshot = StateSnapshotDto.Empty;
    private long _version;

    public StateSnapshotDto GetSnapshot()
    {
        lock (_lock)
        {
            return _snapshot;
        }
    }

    public void Update(StateSnapshotDto nextSnapshot)
    {
        lock (_lock)
        {
            _version++;
            _snapshot = nextSnapshot with
            {
                StateVersion = _version,
                UpdatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
    }
}
