namespace STS2Bridge.Events;

public sealed record GameEvent(
    string EventId,
    string Type,
    string RunId,
    int Floor,
    string? RoomType,
    object Payload)
{
    public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
