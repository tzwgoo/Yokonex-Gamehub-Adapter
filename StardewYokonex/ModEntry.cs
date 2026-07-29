using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewYokonex;

public sealed class ModEntry : Mod
{
    private const string MultiplayerMessageType = "game-event";
    private GatewaySender? sender;
    private long sequence;
    private readonly Dictionary<long, long> receivedSequences = new();
    private readonly Dictionary<string, DateTimeOffset> recentItems = new();

    public override void Entry(IModHelper helper)
    {
        this.sender = new GatewaySender(this.Monitor);
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.DayEnding += this.OnDayEnding;
        helper.Events.Player.InventoryChanged += this.OnInventoryChanged;
        helper.Events.Player.LevelChanged += this.OnLevelChanged;
        helper.Events.Player.Warped += this.OnWarped;
        helper.Events.Multiplayer.PeerConnected += this.OnPeerConnected;
        helper.Events.Multiplayer.PeerDisconnected += this.OnPeerDisconnected;
        helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.receivedSequences.Clear();
        this.Monitor.Log("Yokonex 星露谷联动已加载。", LogLevel.Info);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) => this.Publish(
        "stardew.save_loaded",
        Game1.player.farmName.Value,
        new() { ["saveName"] = Game1.player.Name, ["farmName"] = Game1.player.farmName.Value, ["year"] = Game1.year, ["season"] = Game1.currentSeason, ["day"] = Game1.dayOfMonth },
        true
    );

    private void OnDayStarted(object? sender, DayStartedEventArgs e) => this.Publish(
        "stardew.day_start",
        Game1.currentSeason,
        new() { ["year"] = Game1.year, ["season"] = Game1.currentSeason, ["day"] = Game1.dayOfMonth, ["weather"] = CurrentWeather() },
        true
    );

    private void OnDayEnding(object? sender, DayEndingEventArgs e) => this.Publish(
        "stardew.day_end",
        Game1.currentSeason,
        new() { ["year"] = Game1.year, ["season"] = Game1.currentSeason, ["day"] = Game1.dayOfMonth, ["time"] = Game1.timeOfDay, ["currentMoney"] = Game1.player.Money },
        true
    );

    private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
    {
        if (!e.IsLocalPlayer) return;
        foreach (var group in e.Added.Where(item => item.Stack > 0).GroupBy(item => item.QualifiedItemId))
        {
            var item = group.First();
            var quantity = group.Sum(entry => entry.Stack);
            var duplicateKey = $"{e.Player.UniqueMultiplayerID}:{item.QualifiedItemId}:{quantity}";
            var now = DateTimeOffset.UtcNow;
            if (this.recentItems.TryGetValue(duplicateKey, out var previous) && now - previous < TimeSpan.FromSeconds(1))
                continue;
            this.recentItems[duplicateKey] = now;
            this.Publish("stardew.item_gained", item.DisplayName, new() { ["itemId"] = item.QualifiedItemId, ["name"] = item.DisplayName, ["quantity"] = quantity, ["playerId"] = e.Player.UniqueMultiplayerID }, false);
        }
        foreach (var key in this.recentItems.Where(pair => DateTimeOffset.UtcNow - pair.Value > TimeSpan.FromSeconds(2)).Select(pair => pair.Key).ToArray())
            this.recentItems.Remove(key);
    }

    private void OnLevelChanged(object? sender, LevelChangedEventArgs e)
    {
        if (!e.IsLocalPlayer) return;
        this.Publish("stardew.skill_level_up", e.Skill.ToString(), new() { ["skill"] = e.Skill.ToString(), ["oldLevel"] = e.OldLevel, ["newLevel"] = e.NewLevel, ["playerId"] = e.Player.UniqueMultiplayerID }, true);
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!e.IsLocalPlayer) return;
        this.Publish("stardew.location_changed", e.NewLocation.NameOrUniqueName, new() { ["from"] = e.OldLocation.NameOrUniqueName, ["to"] = e.NewLocation.NameOrUniqueName, ["playerId"] = e.Player.UniqueMultiplayerID }, false);
    }

    private void OnPeerConnected(object? sender, PeerConnectedEventArgs e) => this.Publish(
        "stardew.player_join", e.Peer.PlayerID.ToString(), new() { ["playerId"] = e.Peer.PlayerID }, true
    );

    private void OnPeerDisconnected(object? sender, PeerDisconnectedEventArgs e) => this.Publish(
        "stardew.player_leave", e.Peer.PlayerID.ToString(), new() { ["playerId"] = e.Peer.PlayerID }, true
    );

    private void Publish(string eventKey, string matchValue, Dictionary<string, object?> data, bool highPriority)
    {
        var gameEvent = new PendingGameEvent(eventKey, matchValue, data, DateTimeOffset.UtcNow, $"{Game1.player.UniqueMultiplayerID}-{Interlocked.Increment(ref this.sequence)}", highPriority);
        // 农场工只发给主机，只有主机与桌面网关建立连接。
        if (Context.IsMultiplayer && !Context.IsMainPlayer)
        {
            this.Helper.Multiplayer.SendMessage(
                new MultiplayerEnvelope { PlayerId = Game1.player.UniqueMultiplayerID, Sequence = this.sequence, Event = gameEvent },
                MultiplayerMessageType,
                modIDs: new[] { this.ModManifest.UniqueID }
            );
            return;
        }
        this.sender?.Enqueue(gameEvent);
    }

    private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (!Context.IsMainPlayer || e.FromModID != this.ModManifest.UniqueID || e.Type != MultiplayerMessageType) return;
        var message = e.ReadAs<MultiplayerEnvelope>();
        if (message.Event is null || message.PlayerId != e.FromPlayerID) return;
        if (this.receivedSequences.TryGetValue(e.FromPlayerID, out var last) && message.Sequence <= last) return;
        this.receivedSequences[e.FromPlayerID] = message.Sequence;
        this.sender?.Enqueue(message.Event);
    }

    private static string CurrentWeather()
    {
        if (Game1.isSnowing) return "snow";
        if (Game1.isRaining) return "rain";
        if (Game1.isLightning) return "storm";
        return "sunny";
    }
}
