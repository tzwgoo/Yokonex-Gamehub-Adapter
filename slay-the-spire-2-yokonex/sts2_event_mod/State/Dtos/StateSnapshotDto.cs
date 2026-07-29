namespace STS2Bridge.State.Dtos;

public sealed record StateSnapshotDto
{
    public static readonly StateSnapshotDto Empty = new();

    public long StateVersion { get; init; }

    public long UpdatedAtUnixMs { get; init; }

    public string RunId { get; init; } = string.Empty;

    public string Seed { get; init; } = string.Empty;

    public int Act { get; init; }

    public int Floor { get; init; }

    public string RoomType { get; init; } = string.Empty;

    public ScreenStateDto Screen { get; init; } = new("unknown", null);

    public PlayerStateDto Player { get; init; } = new(0, 0, 0, 0, 0);

    public string LocalPlayerId { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, PlayerStateDto> PlayersById { get; init; } =
        new Dictionary<string, PlayerStateDto>(StringComparer.Ordinal);

    public IReadOnlyList<CardStateDto> Hand { get; init; } = [];

    public IReadOnlyList<EnemyStateDto> Enemies { get; init; } = [];

    public IReadOnlyList<string> Relics { get; init; } = [];

    public IReadOnlyList<string> Potions { get; init; } = [];
}

public sealed record ScreenStateDto(string Name, string? SubState);

public sealed record PlayerStateDto(int Hp, int MaxHp, int Energy, int Block, int Gold);

public sealed record CardStateDto(string InstanceId, string CardId, string Name, int Cost, bool Playable, bool TargetRequired);

public sealed record EnemyStateDto(string InstanceId, string Name, int Hp, int MaxHp, int Block, bool Alive);
