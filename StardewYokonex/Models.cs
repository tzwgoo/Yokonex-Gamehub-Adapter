using System.Text.Json.Serialization;

namespace StardewYokonex;

internal sealed record PendingGameEvent(
    string EventKey,
    string MatchValue,
    Dictionary<string, object?> Data,
    DateTimeOffset OccurredAt,
    string EventId,
    bool HighPriority
);

internal sealed class AdapterConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "ws://127.0.0.1:43002/stardew";

    [JsonPropertyName("mappings")]
    public Dictionary<string, string> Mappings { get; set; } = new();
}

internal sealed class MultiplayerEnvelope
{
    public long PlayerId { get; set; }
    public long Sequence { get; set; }
    public PendingGameEvent? Event { get; set; }
}
