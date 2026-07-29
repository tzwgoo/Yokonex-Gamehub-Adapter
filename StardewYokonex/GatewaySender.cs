using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StardewModdingAPI;

namespace StardewYokonex;

internal sealed class GatewaySender : IDisposable
{
    private const int MaxQueueSize = 200;
    private static readonly Uri ConfigUri = new("http://127.0.0.1:43002/v1/game-integrations/stardew/adapter-config");
    private readonly IMonitor monitor;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private readonly Queue<PendingGameEvent> highPriority = new();
    private readonly Queue<PendingGameEvent> lowPriority = new();
    private readonly SemaphoreSlim available = new(0);
    private readonly CancellationTokenSource stopping = new();
    private readonly object queueLock = new();
    private readonly string sessionId = $"stardew-{Guid.NewGuid():N}";
    private AdapterConfig config = new();
    private ClientWebSocket? socket;
    private int queued;

    public GatewaySender(IMonitor monitor)
    {
        this.monitor = monitor;
        _ = Task.Run(() => this.RunAsync(this.stopping.Token));
    }

    public void Enqueue(PendingGameEvent gameEvent)
    {
        lock (this.queueLock)
        {
            // 队列满时优先保住昏倒、技能升级等高优先级事件。
            if (this.queued >= MaxQueueSize)
            {
                if (!gameEvent.HighPriority || this.lowPriority.Count == 0)
                    return;
                this.lowPriority.Dequeue();
                this.queued--;
            }
            (gameEvent.HighPriority ? this.highPriority : this.lowPriority).Enqueue(gameEvent);
            this.queued++;
        }
        this.available.Release();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var nextConfigRefresh = DateTimeOffset.MinValue;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (DateTimeOffset.UtcNow >= nextConfigRefresh)
                {
                    await this.RefreshConfigAsync(cancellationToken);
                    nextConfigRefresh = DateTimeOffset.UtcNow.AddSeconds(5);
                }
                await this.available.WaitAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
                var gameEvent = this.Dequeue();
                if (gameEvent is null || DateTimeOffset.UtcNow - gameEvent.OccurredAt > TimeSpan.FromSeconds(3))
                    continue;
                if (!this.config.Enabled || !this.config.Mappings.TryGetValue(gameEvent.EventKey, out var commandId) || string.IsNullOrWhiteSpace(commandId))
                    continue;
                await this.EnsureConnectedAsync(cancellationToken);
                var payload = new
                {
                    source = "stardew",
                    eventKey = gameEvent.EventKey,
                    commandId,
                    occurredAt = gameEvent.OccurredAt.ToUniversalTime().ToString("O"),
                    sessionId = this.sessionId,
                    eventId = gameEvent.EventId,
                    matchValue = string.IsNullOrWhiteSpace(gameEvent.MatchValue) ? null : gameEvent.MatchValue,
                    data = gameEvent.Data,
                };
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
                await this.socket!.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                this.monitor.LogOnce($"Yokonex-Gamehub 暂不可用，游戏不会被阻塞：{ex.Message}", LogLevel.Warn);
                await this.ResetSocketAsync();
                try { await Task.Delay(800, cancellationToken); } catch (OperationCanceledException) { }
            }
        }
    }

    private PendingGameEvent? Dequeue()
    {
        lock (this.queueLock)
        {
            PendingGameEvent? item = null;
            if (this.highPriority.Count > 0) item = this.highPriority.Dequeue();
            else if (this.lowPriority.Count > 0) item = this.lowPriority.Dequeue();
            if (item is not null) this.queued--;
            return item;
        }
    }

    private async Task RefreshConfigAsync(CancellationToken cancellationToken)
    {
        var loaded = await this.http.GetFromJsonAsync<AdapterConfig>(ConfigUri, cancellationToken);
        if (loaded is not null) this.config = loaded;
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (this.socket?.State == WebSocketState.Open) return;
        await this.ResetSocketAsync();
        this.socket = new ClientWebSocket();
        await this.socket.ConnectAsync(new Uri(this.config.Endpoint), cancellationToken);
    }

    private async Task ResetSocketAsync()
    {
        var old = this.socket;
        this.socket = null;
        if (old is null) return;
        try
        {
            if (old.State == WebSocketState.Open)
                await old.CloseAsync(WebSocketCloseStatus.NormalClosure, "reset", CancellationToken.None);
        }
        catch { }
        old.Dispose();
    }

    public void Dispose()
    {
        this.stopping.Cancel();
        this.socket?.Dispose();
        this.http.Dispose();
        this.available.Dispose();
        this.stopping.Dispose();
    }
}
