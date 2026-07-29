using STS2Bridge.Events;
using STS2Bridge.Logging;
using STS2Bridge.State;

namespace STS2Bridge.Runtime;

internal static class OrbEventBridgeLogic
{
    private static readonly string[] PassiveValueMemberNames = ["PassiveVal", "passiveVal"];
    private static readonly string[] EvokeValueMemberNames = ["EvokeVal", "evokeVal"];
    private static readonly string[] OwnerMemberNames = ["Owner", "owner"];
    private static readonly string[] OwnerIdMemberNames = ["NetId", "netId", "PlayerId", "playerId"];

    public static bool PublishPassiveTriggered(GameEventBus eventBus, GameStateStore stateStore, object? orb, string orbType, string amountKind)
    {
        return Publish(eventBus, stateStore, orb, GetPassiveEventType(orbType), orbType, amountKind, PassiveValueMemberNames, "passive");
    }

    public static bool PublishEvoked(GameEventBus eventBus, GameStateStore stateStore, object? orb, string orbType, string amountKind)
    {
        return Publish(eventBus, stateStore, orb, GetEvokedEventType(orbType), orbType, amountKind, EvokeValueMemberNames, "evoked");
    }

    private static bool Publish(
        GameEventBus eventBus,
        GameStateStore stateStore,
        object? orb,
        string eventType,
        string orbType,
        string amountKind,
        IReadOnlyList<string> amountMemberNames,
        string triggerKind)
    {
        if (orb is null)
        {
            return false;
        }

        if (!RuntimeReflectionHelpers.TryGetInt(orb, amountMemberNames, out var amount))
        {
            ModLog.Warn($"Orb event skipped because amount could not be resolved. orbType={orbType} triggerKind={triggerKind} runtimeType={orb.GetType().FullName}");
            return false;
        }

        var ownerId = TryGetOwnerId(orb, out var resolvedOwnerId) ? resolvedOwnerId : "unknown";
        var snapshot = stateStore.GetSnapshot();
        eventBus.Publish(new GameEvent(
            EventId: $"evt-{Guid.NewGuid():N}",
            Type: eventType,
            RunId: snapshot.RunId,
            Floor: snapshot.Floor,
            RoomType: snapshot.RoomType,
            Payload: new
            {
                orbType,
                amountKind,
                amount,
                ownerId,
                displayName = $"{orbType}.{triggerKind}"
            }));

        return true;
    }

    private static bool TryGetOwnerId(object orb, out string ownerId)
    {
        ownerId = string.Empty;

        foreach (var ownerMemberName in OwnerMemberNames)
        {
            var owner = RuntimeReflectionHelpers.GetMemberValue(orb, ownerMemberName);
            if (owner is not null &&
                RuntimeReflectionHelpers.TryGetIdentifierString(owner, OwnerIdMemberNames, out ownerId))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetPassiveEventType(string orbType)
    {
        return orbType.ToLowerInvariant() switch
        {
            "lightning" => EventTypes.LightningOrbPassiveTriggered,
            "frost" => EventTypes.FrostOrbPassiveTriggered,
            "dark" => EventTypes.DarkOrbPassiveTriggered,
            "plasma" => EventTypes.PlasmaOrbPassiveTriggered,
            "glass" => EventTypes.GlassOrbPassiveTriggered,
            _ => throw new ArgumentOutOfRangeException(nameof(orbType), orbType, "Unsupported orb type.")
        };
    }

    private static string GetEvokedEventType(string orbType)
    {
        return orbType.ToLowerInvariant() switch
        {
            "lightning" => EventTypes.LightningOrbEvoked,
            "frost" => EventTypes.FrostOrbEvoked,
            "dark" => EventTypes.DarkOrbEvoked,
            "plasma" => EventTypes.PlasmaOrbEvoked,
            "glass" => EventTypes.GlassOrbEvoked,
            _ => throw new ArgumentOutOfRangeException(nameof(orbType), orbType, "Unsupported orb type.")
        };
    }
}
