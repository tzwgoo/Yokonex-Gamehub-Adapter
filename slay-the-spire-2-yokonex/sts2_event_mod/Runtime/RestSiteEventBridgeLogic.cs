using STS2Bridge.Events;
using STS2Bridge.Logging;
using STS2Bridge.State;

namespace STS2Bridge.Runtime;

internal static class RestSiteEventBridgeLogic
{
    private static readonly Lock PublicationLock = new();
    private static readonly string[] PlayerIdMemberNames = ["PlayerId", "playerId"];
    private static readonly string[] UpgradedCardsMemberNames = ["UpgradedCards", "upgradedCards"];
    private static readonly string[] PlayerStatsMemberNames = ["PlayerStats", "playerStats"];
    private static readonly string[] CurrentMapPointHistoryEntryMemberNames = ["CurrentMapPointHistoryEntry", "currentMapPointHistoryEntry"];
    private static readonly string[] CardIdMemberNames = ["Id", "id", "CardId", "cardId", "ModelId", "modelId"];
    private static readonly string[] CardNameMemberNames = ["Name", "name"];
    private static readonly string[] UpgradeLevelMemberNames = ["CurrentUpgradeLevel", "currentUpgradeLevel", "UpgradeLevel", "upgradeLevel"];
    private static string? _lastPublicationKey;
    private static DateTimeOffset _lastPublicationAt;

    public static bool PublishCardUpgraded(GameEventBus eventBus, GameStateStore stateStore, object? runState, object? player)
    {
        if (!TryGetPlayerId(player, out var playerId))
        {
            return false;
        }

        if (!TryFindLastUpgradedCard(runState, playerId, out var upgradedCard))
        {
            return false;
        }

        return PublishSnapshot(
            eventBus,
            stateStore,
            playerId,
            upgradedCard,
            "rest_site_smith",
            1);
    }

    public static bool PublishCardUpgradedFromSource(
        GameEventBus eventBus,
        GameStateStore stateStore,
        object? player,
        object? card,
        string source,
        int upgradeAmount)
    {
        if (!TryGetPlayerId(player, out var playerId) ||
            !TryCreateUpgradedCardSnapshot(card, out var upgradedCard))
        {
            return false;
        }

        return PublishSnapshot(eventBus, stateStore, playerId, upgradedCard, source, upgradeAmount);
    }

    public static object? FindRunStateArgument(object?[]? args)
    {
        if (args is null)
        {
            return null;
        }

        foreach (var arg in args)
        {
            if (arg is not null && RuntimeReflectionHelpers.GetMemberValue(arg, CurrentMapPointHistoryEntryMemberNames[0]) is not null)
            {
                return arg;
            }
        }

        return null;
    }

    public static object? FindPlayerArgument(object?[]? args)
    {
        if (args is null)
        {
            return null;
        }

        foreach (var arg in args)
        {
            if (TryGetPlayerId(arg, out _))
            {
                return arg;
            }
        }

        return null;
    }

    public static object? FindCardArgument(object?[]? args)
    {
        if (args is null)
        {
            return null;
        }

        foreach (var arg in args)
        {
            if (TryCreateUpgradedCardSnapshot(arg, out _))
            {
                return arg;
            }
        }

        return null;
    }

    private static bool PublishSnapshot(
        GameEventBus eventBus,
        GameStateStore stateStore,
        string playerId,
        UpgradedCardSnapshot upgradedCard,
        string source,
        int upgradeAmount)
    {
        if (upgradeAmount <= 0 || ShouldSkipDuplicate(playerId, upgradedCard.CardId, upgradedCard.UpgradeLevelAfter))
        {
            return false;
        }

        var state = stateStore.GetSnapshot();
        eventBus.Publish(new GameEvent(
            EventId: $"evt-{Guid.NewGuid():N}",
            Type: EventTypes.CardUpgraded,
            RunId: state.RunId,
            Floor: state.Floor,
            RoomType: state.RoomType,
            Payload: new
            {
                playerId,
                cardId = upgradedCard.CardId,
                cardName = upgradedCard.CardName,
                upgradeLevelBefore = Math.Max(0, upgradedCard.UpgradeLevelAfter - upgradeAmount),
                upgradeLevelAfter = upgradedCard.UpgradeLevelAfter,
                source
            }));

        return true;
    }

    private static bool TryFindLastUpgradedCard(object? runState, string playerId, out UpgradedCardSnapshot snapshot)
    {
        snapshot = default;
        var mapPointHistoryEntry = GetFirstExistingMember(runState, CurrentMapPointHistoryEntryMemberNames);
        var playerStats = GetEnumerable(GetFirstExistingMember(mapPointHistoryEntry, PlayerStatsMemberNames));
        if (playerStats is null)
        {
            return false;
        }

        foreach (var playerEntry in playerStats)
        {
            if (!TryGetPlayerId(playerEntry, out var entryPlayerId) || !string.Equals(entryPlayerId, playerId, StringComparison.Ordinal))
            {
                continue;
            }

            var upgradedCards = GetEnumerable(GetFirstExistingMember(playerEntry, UpgradedCardsMemberNames));
            if (upgradedCards is null)
            {
                continue;
            }

            UpgradedCardSnapshot? last = null;
            foreach (var upgradedCard in upgradedCards)
            {
                if (TryCreateUpgradedCardSnapshot(upgradedCard, out var current))
                {
                    last = current;
                }
            }

            if (last is not null)
            {
                snapshot = last.Value;
                return true;
            }
        }

        ModLog.Warn("Rest site upgrade event skipped because no upgraded card history was found for the player.");
        return false;
    }

    private static bool TryCreateUpgradedCardSnapshot(object? card, out UpgradedCardSnapshot snapshot)
    {
        snapshot = default;
        if (card is null)
        {
            return false;
        }

        RuntimeReflectionHelpers.TryGetString(card, CardIdMemberNames, out var cardId);
        RuntimeReflectionHelpers.TryGetString(card, CardNameMemberNames, out var cardName);
        RuntimeReflectionHelpers.TryGetInt(card, UpgradeLevelMemberNames, out var upgradeLevelAfter);
        if (string.IsNullOrWhiteSpace(cardId) && string.IsNullOrWhiteSpace(cardName))
        {
            return false;
        }

        snapshot = new UpgradedCardSnapshot(
            string.IsNullOrWhiteSpace(cardId) ? null : cardId,
            string.IsNullOrWhiteSpace(cardName) ? null : cardName,
            upgradeLevelAfter);
        return true;
    }

    private static bool ShouldSkipDuplicate(string playerId, string? cardId, int upgradeLevelAfter)
    {
        var key = $"{playerId}|{cardId ?? string.Empty}|{upgradeLevelAfter}";
        var now = DateTimeOffset.UtcNow;
        lock (PublicationLock)
        {
            if (string.Equals(_lastPublicationKey, key, StringComparison.Ordinal) &&
                now - _lastPublicationAt < TimeSpan.FromMilliseconds(750))
            {
                return true;
            }

            _lastPublicationKey = key;
            _lastPublicationAt = now;
            return false;
        }
    }

    private static bool TryGetPlayerId(object? player, out string playerId)
    {
        return RuntimeReflectionHelpers.TryGetString(player, PlayerIdMemberNames, out playerId);
    }

    private static object? GetFirstExistingMember(object? instance, IReadOnlyList<string> memberNames)
    {
        if (instance is null)
        {
            return null;
        }

        foreach (var memberName in memberNames)
        {
            var value = RuntimeReflectionHelpers.GetMemberValue(instance, memberName);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<object>? GetEnumerable(object? instance)
    {
        if (instance is null || instance is string)
        {
            return null;
        }

        if (instance is System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object>();
        }

        return null;
    }

    private readonly record struct UpgradedCardSnapshot(string? CardId, string? CardName, int UpgradeLevelAfter);
}
