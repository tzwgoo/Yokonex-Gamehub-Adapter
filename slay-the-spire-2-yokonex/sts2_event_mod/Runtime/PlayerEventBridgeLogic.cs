using STS2Bridge.Events;
using STS2Bridge.Logging;
using STS2Bridge.State;

namespace STS2Bridge.Runtime;

internal static class PlayerEventBridgeLogic
{
    private static readonly string[] PlayerIdMemberNames = ["PlayerId", "playerId", "NetId", "netId"];
    private static readonly string[] CurrentHpMemberNames = ["CurrentHp", "currentHp"];
    private static readonly string[] MaxHpMemberNames = ["MaxHp", "maxHp"];
    private static readonly string[] BlockMemberNames = ["Block", "block"];
    private static readonly string[] CreatureMemberNames = ["Creature", "creature"];
    private static readonly string[] CombatStateMemberNames = ["CombatState", "combatState"];
    private static readonly string[] PlayerCreaturesMemberNames = ["PlayerCreatures", "playerCreatures"];
    private static readonly string[] PlayersMemberNames = ["Players", "players"];
    private static readonly string[] NonPlayerMarkers = ["MonsterId", "monsterId", "EnemyId", "enemyId"];
    private static readonly string[] NestedStateMemberNames = ["State", "state", "_state", "CreatureState", "creatureState", "PlayerState", "playerState"];
    private static readonly string[] BlockedDamageMemberNames = ["BlockedDamage", "blockedDamage"];
    private static readonly string[] UnblockedDamageMemberNames = ["UnblockedDamage", "unblockedDamage"];
    private static readonly string[] WasBlockBrokenMemberNames = ["WasBlockBroken", "wasBlockBroken"];
    private static readonly string[] WasFullyBlockedMemberNames = ["WasFullyBlocked", "wasFullyBlocked"];

    public static bool PublishHpChanged(GameEventBus eventBus, GameStateStore stateStore, object? creature, int delta)
    {
        if (!TryCreatePlayerSnapshot(creature, out var player))
        {
            return false;
        }

        UpdateState(stateStore, player);

        if (delta < 0)
        {
            PlayerDamageTransitionCache.Store(creature, player.CurrentHp, Math.Abs(delta));
            Publish(
                eventBus,
                stateStore,
                EventTypes.PlayerDamaged,
                player,
                new
                {
                    playerId = player.PlayerId,
                    amount = Math.Abs(delta),
                    currentHp = player.CurrentHp,
                    maxHp = player.MaxHp,
                    block = player.Block
                },
                updateState: false);
        }
        else if (delta > 0)
        {
            Publish(
                eventBus,
                stateStore,
                EventTypes.PlayerHealed,
                player,
                new
                {
                    playerId = player.PlayerId,
                    amount = delta,
                    currentHp = player.CurrentHp,
                    maxHp = player.MaxHp,
                    block = player.Block
                },
                updateState: false);
        }

        return true;
    }

    public static bool PublishBlockChanged(GameEventBus eventBus, GameStateStore stateStore, object? creature, int? delta, string reason)
    {
        if (!TryCreatePlayerSnapshot(creature, out var player))
        {
            return false;
        }

        UpdateState(stateStore, player);

        return true;
    }

    public static bool RefreshBlockCleared(GameStateStore stateStore, object? creature)
    {
        if (!TryCreatePlayerSnapshot(creature, out var player))
        {
            return false;
        }

        UpdateState(stateStore, player);

        return true;
    }

    public static bool PublishBlockLossFromTransition(GameEventBus eventBus, GameStateStore stateStore, object? creature, int previousBlock, string reason)
    {
        if (!TryCreatePlayerSnapshot(creature, out var player))
        {
            return false;
        }

        var delta = player.Block - previousBlock;
        if (delta >= 0)
        {
            return false;
        }

        UpdateState(stateStore, player);

        return true;
    }

    public static bool PublishBlockBrokenFromTransition(GameEventBus eventBus, GameStateStore stateStore, object? creature, int previousBlock)
    {
        if (!TryCreatePlayerSnapshot(creature, out var player))
        {
            return false;
        }

        var delta = player.Block - previousBlock;
        if (previousBlock <= 0 || delta >= 0)
        {
            return false;
        }

        Publish(
            eventBus,
            stateStore,
            EventTypes.PlayerBlockBroken,
            player,
            new
            {
                playerId = player.PlayerId,
                previousBlock,
                block = player.Block,
                currentHp = player.CurrentHp,
                maxHp = player.MaxHp
            },
            updateState: false);

        return true;
    }

    public static bool PublishPlayerDied(GameEventBus eventBus, GameStateStore stateStore, object? creature, bool wasRemovalPrevented)
    {
        if (!TryCreatePlayerSnapshot(creature, out var player))
        {
            return false;
        }

        Publish(
            eventBus,
            stateStore,
            EventTypes.PlayerDied,
            player,
            new
            {
                playerId = player.PlayerId,
                currentHp = player.CurrentHp,
                maxHp = player.MaxHp,
                block = player.Block,
                wasRemovalPrevented
            });

        return true;
    }

    public static bool PublishDamageReceived(GameEventBus eventBus, GameStateStore stateStore, object? creature, object? result)
    {
        if (!TryCreatePlayerSnapshot(creature, out var player) || !TryCreateDamageSnapshot(result, out var damage))
        {
            return false;
        }

        var published = false;
        if (damage.UnblockedDamage > 0)
        {
            var wasRecentlyPublishedByHpChange =
                PlayerDamageTransitionCache.TryConsume(creature, player.CurrentHp, damage.UnblockedDamage);

            if (!wasRecentlyPublishedByHpChange)
            {
                Publish(
                    eventBus,
                    stateStore,
                    EventTypes.PlayerDamaged,
                    player,
                    new
                    {
                        playerId = player.PlayerId,
                        amount = damage.UnblockedDamage,
                        currentHp = player.CurrentHp,
                        maxHp = player.MaxHp,
                        block = player.Block
                    });
                published = true;
            }
        }

        if (damage.BlockedDamage > 0)
        {
            PlayerBlockTransitionCache.TryConsume(creature, out _);
        }

        return published;
    }

    public static bool TryGetBlockValue(object? creature, out int block)
    {
        return TryGetPlayerIntValue(creature, BlockMemberNames, out block);
    }

    public static bool IsPlayerCreature(object? creature)
    {
        return TryCreatePlayerSnapshot(creature, out _);
    }

    private static void Publish(
        GameEventBus eventBus,
        GameStateStore stateStore,
        string eventType,
        PlayerSnapshot player,
        object payload,
        bool updateState = true)
    {
        if (updateState)
        {
            UpdateState(stateStore, player);
        }

        var snapshot = stateStore.GetSnapshot();
        eventBus.Publish(new GameEvent(
            EventId: $"evt-{Guid.NewGuid():N}",
            Type: eventType,
            RunId: snapshot.RunId,
            Floor: snapshot.Floor,
            RoomType: snapshot.RoomType,
            Payload: payload));
    }

    private static void UpdateState(GameStateStore stateStore, PlayerSnapshot player)
    {
        var snapshot = stateStore.GetSnapshot();
        var playersById = PlayerStateSnapshotMap.Upsert(snapshot.PlayersById, player.PlayerId, existing => existing with
        {
            Hp = player.CurrentHp,
            MaxHp = player.MaxHp,
            Block = player.Block
        });
        stateStore.Update(snapshot with
        {
            LocalPlayerId = LocalPlayerRuntimeResolver.ResolveOrKeep(snapshot.LocalPlayerId),
            Player = snapshot.Player with
            {
                Hp = player.CurrentHp,
                MaxHp = player.MaxHp,
                Block = player.Block
            },
            PlayersById = playersById
        });
        PlayerRuntimeStateCache.StoreBlock(player.PlayerId, player.Block);
    }

    private static bool TryCreatePlayerSnapshot(object? creature, out PlayerSnapshot player)
    {
        player = default;
        if (creature is null)
        {
            return false;
        }

        if (HasAnyMember(creature, NonPlayerMarkers) || NestedStateHasAnyMember(creature, NonPlayerMarkers))
        {
            return false;
        }

        if (!TryResolvePlayerId(creature, out var playerId))
        {
            ModLog.Warn($"Player event skipped because playerId could not be resolved on '{creature.GetType().FullName}'.");
            return false;
        }

        if (!TryGetPlayerIntValue(creature, CurrentHpMemberNames, out var currentHp) ||
            !TryGetPlayerIntValue(creature, MaxHpMemberNames, out var maxHp) ||
            !TryGetPlayerIntValue(creature, BlockMemberNames, out var block))
        {
            ModLog.Warn($"Player event skipped because required members were missing on '{creature.GetType().FullName}'.");
            return false;
        }

        player = new PlayerSnapshot(playerId, currentHp, maxHp, block);
        return true;
    }

    private static bool TryCreateDamageSnapshot(object? result, out DamageSnapshot snapshot)
    {
        snapshot = default;
        if (result is null)
        {
            return false;
        }

        if (!RuntimeReflectionHelpers.TryGetInt(result, BlockedDamageMemberNames, out var blockedDamage) ||
            !RuntimeReflectionHelpers.TryGetInt(result, UnblockedDamageMemberNames, out var unblockedDamage) ||
            !RuntimeReflectionHelpers.TryGetBool(result, WasBlockBrokenMemberNames, out var wasBlockBroken) ||
            !RuntimeReflectionHelpers.TryGetBool(result, WasFullyBlockedMemberNames, out var wasFullyBlocked))
        {
            return false;
        }

        snapshot = new DamageSnapshot(blockedDamage, unblockedDamage, wasBlockBroken, wasFullyBlocked);
        return true;
    }

    private static bool HasAnyMember(object instance, IReadOnlyList<string> memberNames)
    {
        foreach (var memberName in memberNames)
        {
            if (RuntimeReflectionHelpers.GetMemberValue(instance, memberName) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool NestedStateHasAnyMember(object instance, IReadOnlyList<string> memberNames)
    {
        if (!TryGetNestedState(instance, out var nestedState))
        {
            return false;
        }

        return HasAnyMember(nestedState, memberNames);
    }

    internal static bool TryResolvePlayerId(object creature, out string playerId)
    {
        playerId = string.Empty;

        if (RuntimeReflectionHelpers.TryGetIdentifierString(creature, PlayerIdMemberNames, out playerId))
        {
            return true;
        }

        if (TryGetNestedState(creature, out var nestedState) &&
            RuntimeReflectionHelpers.TryGetIdentifierString(nestedState, PlayerIdMemberNames, out playerId))
        {
            return true;
        }

        if (TryResolvePlayerIdFromCombatState(creature, out playerId))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetPlayerIntValue(object? creature, IReadOnlyList<string> memberNames, out int value)
    {
        value = default;
        if (creature is null)
        {
            return false;
        }

        if (RuntimeReflectionHelpers.TryGetInt(creature, memberNames, out value))
        {
            return true;
        }

        return TryGetNestedState(creature, out var nestedState) &&
            RuntimeReflectionHelpers.TryGetInt(nestedState, memberNames, out value);
    }

    private static bool TryGetNestedState(object creature, out object nestedState)
    {
        nestedState = null!;

        foreach (var memberName in NestedStateMemberNames)
        {
            var candidate = RuntimeReflectionHelpers.GetMemberValue(creature, memberName);
            if (candidate is not null)
            {
                nestedState = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolvePlayerIdFromCombatState(object creature, out string playerId)
    {
        playerId = string.Empty;
        if (!TryGetCombatState(creature, out var combatState))
        {
            return false;
        }

        if (!TryGetEnumerable(combatState, PlayersMemberNames, out var players))
        {
            return false;
        }

        var playerList = players.Cast<object?>().Where(static player => player is not null).ToList();
        foreach (var player in playerList)
        {
            if (PlayerMatchesCreature(player!, creature) &&
                RuntimeReflectionHelpers.TryGetIdentifierString(player, PlayerIdMemberNames, out playerId))
            {
                return true;
            }
        }

        if (!TryGetEnumerable(combatState, PlayerCreaturesMemberNames, out var playerCreatures))
        {
            return false;
        }

        var creatureIndex = 0;
        foreach (var candidate in playerCreatures)
        {
            if (CreaturesMatch(candidate, creature))
            {
                var player = playerList.ElementAtOrDefault(creatureIndex);
                return RuntimeReflectionHelpers.TryGetIdentifierString(player, PlayerIdMemberNames, out playerId) ||
                    (player is not null && TryGetNestedState(player, out var nestedState) &&
                     RuntimeReflectionHelpers.TryGetIdentifierString(nestedState, PlayerIdMemberNames, out playerId));
            }

            creatureIndex++;
        }

        return false;
    }

    private static bool TryGetCombatState(object creature, out object combatState)
    {
        combatState = null!;
        foreach (var memberName in CombatStateMemberNames)
        {
            var candidate = RuntimeReflectionHelpers.GetMemberValue(creature, memberName);
            if (candidate is not null)
            {
                combatState = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetEnumerable(object instance, IReadOnlyList<string> memberNames, out System.Collections.IEnumerable values)
    {
        values = null!;
        foreach (var memberName in memberNames)
        {
            if (RuntimeReflectionHelpers.GetMemberValue(instance, memberName) is System.Collections.IEnumerable enumerable)
            {
                values = enumerable;
                return true;
            }
        }

        return false;
    }

    private static bool PlayerMatchesCreature(object player, object creature)
    {
        var playerCreature = GetPlayerCreature(player);
        return playerCreature is not null && CreaturesMatch(playerCreature, creature);
    }

    private static object? GetPlayerCreature(object player)
    {
        foreach (var memberName in CreatureMemberNames)
        {
            var candidate = RuntimeReflectionHelpers.GetMemberValue(player, memberName);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool CreaturesMatch(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return ReferenceEquals(left, right);
    }

    private readonly record struct PlayerSnapshot(string PlayerId, int CurrentHp, int MaxHp, int Block);

    private readonly record struct DamageSnapshot(int BlockedDamage, int UnblockedDamage, bool WasBlockBroken, bool WasFullyBlocked);
}
