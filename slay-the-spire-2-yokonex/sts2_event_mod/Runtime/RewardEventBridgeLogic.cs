using STS2Bridge.Events;
using STS2Bridge.Logging;
using STS2Bridge.State;

namespace STS2Bridge.Runtime;

internal static class RewardEventBridgeLogic
{
    private static readonly string[] PlayerIdMemberNames = ["PlayerId", "playerId", "Id", "id", "CharacterId", "characterId", "Character", "character", "NetId", "netId"];
    private static readonly string[] RewardPlayerMemberNames = ["Player", "player"];
    private static readonly string[] RewardTypeMemberNames = ["RewardType", "rewardType", "Type", "type"];
    private static readonly string[] RewardIdMemberNames = ["Id", "id", "CardId", "cardId", "CardModelId", "cardModelId", "ModelId", "modelId", "PredeterminedModelId", "predeterminedModelId"];
    private static readonly string[] RewardNameMemberNames = ["Name", "name", "Title", "title", "DisplayName", "displayName", "TitleLocString", "titleLocString", "Description", "description"];
    private static readonly string[] GoldAmountMemberNames = ["GoldAmount", "goldAmount", "Amount", "amount"];
    private static readonly string[] SourceMemberNames = ["Source", "source", "CustomDescriptionEncounterSourceId", "customDescriptionEncounterSourceId"];
    private static readonly string[] OptionCountMemberNames = ["OptionCount", "optionCount"];
    private static readonly string[] WasGoldStolenBackMemberNames = ["WasGoldStolenBack", "wasGoldStolenBack"];
    private static readonly string[] CardsMemberNames = ["Cards", "cards", "Options", "options"];
    private static readonly string[] NestedRewardSourceMembers = ["SpecialCard", "specialCard", "_card", "Card", "card", "ClaimedRelic", "claimedRelic", "Relic", "relic", "ClaimedPotion", "claimedPotion", "Potion", "potion", "Model", "model", "CreationResult", "creationResult"];

    public static bool PublishRewardSelected(GameEventBus eventBus, GameStateStore stateStore, object? player, object? reward)
    {
        if (!TryResolvePlayerId(player, reward, out var playerId))
        {
            return false;
        }

        if (!TryCreateRewardSnapshot(reward, out var rewardSnapshot))
        {
            return false;
        }

        var state = stateStore.GetSnapshot();
        eventBus.Publish(new GameEvent(
            EventId: $"evt-{Guid.NewGuid():N}",
            Type: EventTypes.RewardSelected,
            RunId: state.RunId,
            Floor: state.Floor,
            RoomType: state.RoomType,
            Payload: new
            {
                playerId,
                rewardType = rewardSnapshot.RewardType,
                rewardItemType = rewardSnapshot.RewardItemType,
                rewardId = rewardSnapshot.RewardId,
                rewardName = rewardSnapshot.RewardName,
                goldAmount = rewardSnapshot.GoldAmount,
                rewardSource = rewardSnapshot.RewardSource,
                optionCount = rewardSnapshot.OptionCount,
                cardCount = rewardSnapshot.CardCount,
                wasGoldStolenBack = rewardSnapshot.WasGoldStolenBack
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

    public static object? FindRewardArgument(object?[]? args)
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

            if (TryCreateRewardSnapshot(arg, out _))
            {
                return arg;
            }
        }

        return null;
    }

    private static bool TryCreateRewardSnapshot(object? reward, out RewardSnapshot snapshot)
    {
        snapshot = default;
        if (reward is null)
        {
            return false;
        }

        if (!TryGetRewardType(reward, out var rewardType))
        {
            rewardType = DetectRewardItemType(string.Empty, reward, ResolveRewardSource(reward));
            if (rewardType == "other")
            {
                ModLog.Warn($"Reward event skipped because reward type was missing on '{reward.GetType().FullName}'.");
                return false;
            }
        }

        RuntimeReflectionHelpers.TryGetInt(reward, GoldAmountMemberNames, out var goldAmount);
        RuntimeReflectionHelpers.TryGetInt(reward, OptionCountMemberNames, out var optionCount);
        RuntimeReflectionHelpers.TryGetBool(reward, WasGoldStolenBackMemberNames, out var wasGoldStolenBack);
        RuntimeReflectionHelpers.TryGetIdentifierString(reward, SourceMemberNames, out var rewardSource);
        var source = ResolveRewardSource(reward);
        RuntimeReflectionHelpers.TryGetIdentifierString(source, RewardIdMemberNames, out var rewardId);
        RuntimeReflectionHelpers.TryGetDisplayString(source, RewardNameMemberNames, out var rewardName);
        var cardCount = GetEnumerableCount(GetFirstExistingMember(reward, CardsMemberNames));

        snapshot = new RewardSnapshot(
            NormalizeRewardType(rewardType),
            DetectRewardItemType(rewardType, reward, source),
            string.IsNullOrWhiteSpace(rewardId) ? null : rewardId,
            string.IsNullOrWhiteSpace(rewardName) ? null : rewardName,
            goldAmount,
            string.IsNullOrWhiteSpace(rewardSource) ? null : rewardSource,
            optionCount,
            cardCount,
            wasGoldStolenBack);
        return true;
    }

    private static object ResolveRewardSource(object reward)
    {
        return ResolveRewardSource(reward, depth: 0);
    }

    private static object ResolveRewardSource(object reward, int depth)
    {
        if (depth >= 4)
        {
            return reward;
        }

        foreach (var memberName in NestedRewardSourceMembers)
        {
            var nested = RuntimeReflectionHelpers.GetMemberValue(reward, memberName);
            if (nested is null)
            {
                continue;
            }

            if (RuntimeReflectionHelpers.TryGetIdentifierString(nested, RewardIdMemberNames, out _) ||
                RuntimeReflectionHelpers.TryGetDisplayString(nested, RewardNameMemberNames, out _))
            {
                return nested;
            }

            var resolved = ResolveRewardSource(nested, depth + 1);
            if (!ReferenceEquals(resolved, nested))
            {
                return resolved;
            }
        }

        return reward;
    }

    private static bool TryGetPlayerId(object? player, out string playerId)
    {
        return RuntimeReflectionHelpers.TryGetIdentifierString(player, PlayerIdMemberNames, out playerId);
    }

    private static bool TryResolvePlayerId(object? player, object? reward, out string playerId)
    {
        if (TryGetPlayerId(player, out playerId))
        {
            return true;
        }

        var rewardPlayer = GetFirstExistingMember(reward, RewardPlayerMemberNames);
        return TryGetPlayerId(rewardPlayer, out playerId);
    }

    private static bool TryGetRewardType(object reward, out string rewardType)
    {
        rewardType = string.Empty;
        foreach (var memberName in RewardTypeMemberNames)
        {
            var value = RuntimeReflectionHelpers.GetMemberValue(reward, memberName);
            if (value is null)
            {
                continue;
            }

            rewardType = value.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(rewardType))
            {
                return true;
            }
        }

        return false;
    }

    private static string DetectRewardItemType(string rewardType, object reward, object source)
    {
        var normalizedRewardType = NormalizeRewardType(rewardType);
        if (normalizedRewardType.Contains("gold", StringComparison.Ordinal))
        {
            return "gold";
        }

        var typeNames = new[]
        {
            reward.GetType().FullName ?? reward.GetType().Name,
            source.GetType().FullName ?? source.GetType().Name,
            rewardType
        };

        foreach (var typeName in typeNames)
        {
            var normalized = typeName.ToLowerInvariant();
            if (normalized.Contains("potion", StringComparison.Ordinal))
            {
                return "potion";
            }

            if (normalized.Contains("relic", StringComparison.Ordinal))
            {
                return "relic";
            }

            if (normalized.Contains("card", StringComparison.Ordinal))
            {
                return "card";
            }
        }

        return "other";
    }

    private static string NormalizeRewardType(string rewardType)
    {
        return rewardType.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
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

    private static int GetEnumerableCount(object? instance)
    {
        if (instance is null || instance is string)
        {
            return 0;
        }

        if (instance is System.Collections.ICollection collection)
        {
            return collection.Count;
        }

        if (instance is System.Collections.IEnumerable enumerable)
        {
            var count = 0;
            foreach (var _ in enumerable)
            {
                count++;
            }

            return count;
        }

        return 0;
    }

    private readonly record struct RewardSnapshot(
        string RewardType,
        string RewardItemType,
        string? RewardId,
        string? RewardName,
        int GoldAmount,
        string? RewardSource,
        int OptionCount,
        int CardCount,
        bool WasGoldStolenBack);
}
