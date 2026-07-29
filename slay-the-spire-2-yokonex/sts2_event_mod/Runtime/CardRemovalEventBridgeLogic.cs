using STS2Bridge.Events;
using STS2Bridge.Logging;
using STS2Bridge.State;

namespace STS2Bridge.Runtime;

internal static class CardRemovalEventBridgeLogic
{
    private static readonly string[] PlayerIdMemberNames = ["PlayerId", "playerId", "Id", "id", "CharacterId", "characterId", "Character", "character", "NetId", "netId"];
    private static readonly string[] CardPlayerMemberNames = ["Player", "player", "Owner", "owner"];
    private static readonly string[] CardIdMemberNames = ["Id", "id", "CardId", "cardId", "CardModelId", "cardModelId", "ModelId", "modelId"];
    private static readonly string[] CardNameMemberNames = ["Name", "name", "Title", "title", "DisplayName", "displayName", "TitleLocString", "titleLocString"];
    private static readonly string[] NestedCardMemberNames = ["Card", "card", "Model", "model", "CreationResult", "creationResult"];

    public static bool PublishCardRemoved(
        GameEventBus eventBus,
        GameStateStore stateStore,
        object? player,
        object? card,
        string source,
        int goldSpent)
    {
        if (!TryCreateCardSnapshot(card, out var cardSnapshot))
        {
            return false;
        }

        var playerId = TryResolvePlayerId(player, card, out var resolvedPlayerId)
            ? resolvedPlayerId
            : "unknown";

        var state = stateStore.GetSnapshot();
        eventBus.Publish(new GameEvent(
            EventId: $"evt-{Guid.NewGuid():N}",
            Type: EventTypes.CardRemoved,
            RunId: state.RunId,
            Floor: state.Floor,
            RoomType: state.RoomType,
            Payload: new
            {
                playerId,
                cardId = cardSnapshot.CardId,
                cardName = cardSnapshot.CardName,
                source = string.IsNullOrWhiteSpace(source) ? "card_removed" : source,
                goldSpent = Math.Max(0, goldSpent)
            }));

        return true;
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
            if (TryGetPlayerId(arg, out _))
            {
                continue;
            }

            if (TryCreateCardSnapshot(arg, out _))
            {
                return arg;
            }
        }

        return null;
    }

    public static int FindGoldSpentArgument(object?[]? args)
    {
        if (args is null)
        {
            return 0;
        }

        for (var index = args.Length - 1; index >= 0; index--)
        {
            if (args[index] is int goldSpent)
            {
                return goldSpent;
            }
        }

        return 0;
    }

    public static string InferSource(object?[]? args)
    {
        if (args is null)
        {
            return "card_removed";
        }

        foreach (var arg in args)
        {
            var typeName = arg?.GetType().FullName ?? string.Empty;
            if (typeName.Contains("Merchant", StringComparison.OrdinalIgnoreCase))
            {
                return "merchant_removal";
            }

            if (typeName.Contains("Reward", StringComparison.OrdinalIgnoreCase))
            {
                return "reward_removal";
            }

            if (typeName.Contains("Event", StringComparison.OrdinalIgnoreCase))
            {
                return "event_removal";
            }
        }

        return "card_removed";
    }

    private static bool TryCreateCardSnapshot(object? card, out CardSnapshot snapshot)
    {
        snapshot = default;
        if (card is null)
        {
            return false;
        }

        var source = ResolveCardSource(card);
        RuntimeReflectionHelpers.TryGetIdentifierString(source, CardIdMemberNames, out var cardId);
        RuntimeReflectionHelpers.TryGetDisplayString(source, CardNameMemberNames, out var cardName);
        if (string.IsNullOrWhiteSpace(cardId) && string.IsNullOrWhiteSpace(cardName))
        {
            ModLog.Warn($"Card removed event skipped because card details were missing on '{card.GetType().FullName}'.");
            return false;
        }

        snapshot = new CardSnapshot(
            string.IsNullOrWhiteSpace(cardId) ? null : cardId,
            string.IsNullOrWhiteSpace(cardName) ? null : cardName);
        return true;
    }

    private static object ResolveCardSource(object card)
    {
        return ResolveCardSource(card, depth: 0);
    }

    private static object ResolveCardSource(object card, int depth)
    {
        if (depth >= 4)
        {
            return card;
        }

        foreach (var memberName in NestedCardMemberNames)
        {
            var nested = RuntimeReflectionHelpers.GetMemberValue(card, memberName);
            if (nested is null)
            {
                continue;
            }

            if (RuntimeReflectionHelpers.TryGetIdentifierString(nested, CardIdMemberNames, out _) ||
                RuntimeReflectionHelpers.TryGetDisplayString(nested, CardNameMemberNames, out _))
            {
                return nested;
            }

            var resolved = ResolveCardSource(nested, depth + 1);
            if (!ReferenceEquals(resolved, nested))
            {
                return resolved;
            }
        }

        return card;
    }

    private static bool TryResolvePlayerId(object? player, object? card, out string playerId)
    {
        if (TryGetPlayerId(player, out playerId))
        {
            return true;
        }

        foreach (var memberName in CardPlayerMemberNames)
        {
            var cardPlayer = RuntimeReflectionHelpers.GetMemberValue(card, memberName);
            if (TryGetPlayerId(cardPlayer, out playerId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetPlayerId(object? player, out string playerId)
    {
        return RuntimeReflectionHelpers.TryGetIdentifierString(player, PlayerIdMemberNames, out playerId);
    }

    private readonly record struct CardSnapshot(string? CardId, string? CardName);
}
