using STS2Bridge.Events;
using STS2Bridge.Logging;
using STS2Bridge.State;

namespace STS2Bridge.Runtime;

internal static class EventEncounteredEventBridgeLogic
{
    private static readonly string[] EventIdMemberNames = ["EventId", "eventId", "Id", "id", "ModelId", "modelId"];
    private static readonly string[] EventNameMemberNames = ["Name", "name", "Title", "title", "DisplayName", "displayName", "TitleLocString", "titleLocString"];
    private static readonly string[] NestedEventMemberNames = ["CanonicalEvent", "canonicalEvent", "LocalMutableEvent", "localMutableEvent", "Event", "event", "CanonicalEncounter", "canonicalEncounter", "Encounter", "encounter"];

    public static bool PublishEventEncountered(
        GameEventBus eventBus,
        GameStateStore stateStore,
        object? eventRoom,
        bool isRestoringRoomStackBase)
    {
        if (!TryCreateEventSnapshot(eventRoom, out var eventSnapshot))
        {
            return false;
        }

        var state = stateStore.GetSnapshot();
        eventBus.Publish(new GameEvent(
            EventId: $"evt-{Guid.NewGuid():N}",
            Type: EventTypes.EventEncountered,
            RunId: state.RunId,
            Floor: state.Floor,
            RoomType: state.RoomType,
            Payload: new
            {
                eventId = eventSnapshot.EventId,
                eventName = eventSnapshot.EventName,
                source = "event_room_entered",
                isRestoringRoomStackBase
            }));

        return true;
    }

    public static bool FindIsRestoringRoomStackBaseArgument(object?[]? args)
    {
        if (args is null)
        {
            return false;
        }

        foreach (var arg in args)
        {
            if (arg is bool value)
            {
                return value;
            }
        }

        return false;
    }

    private static bool TryCreateEventSnapshot(object? eventRoom, out EventSnapshot snapshot)
    {
        snapshot = default;
        if (eventRoom is null)
        {
            return false;
        }

        RuntimeReflectionHelpers.TryGetIdentifierString(eventRoom, EventIdMemberNames, out var eventId);
        RuntimeReflectionHelpers.TryGetDisplayString(eventRoom, EventNameMemberNames, out var eventName);

        var source = ResolveEventSource(eventRoom);
        if (string.IsNullOrWhiteSpace(eventId))
        {
            RuntimeReflectionHelpers.TryGetIdentifierString(source, EventIdMemberNames, out eventId);
        }

        if (string.IsNullOrWhiteSpace(eventName))
        {
            RuntimeReflectionHelpers.TryGetDisplayString(source, EventNameMemberNames, out eventName);
        }

        if (string.IsNullOrWhiteSpace(eventId) && string.IsNullOrWhiteSpace(eventName))
        {
            ModLog.Warn($"Event encountered event skipped because event details were missing on '{eventRoom.GetType().FullName}'.");
            return false;
        }

        snapshot = new EventSnapshot(
            string.IsNullOrWhiteSpace(eventId) ? "unknown" : eventId,
            string.IsNullOrWhiteSpace(eventName) ? "unknown" : eventName);
        return true;
    }

    private static object ResolveEventSource(object eventRoom)
    {
        return ResolveEventSource(eventRoom, depth: 0);
    }

    private static object ResolveEventSource(object eventRoom, int depth)
    {
        if (depth >= 4)
        {
            return eventRoom;
        }

        foreach (var memberName in NestedEventMemberNames)
        {
            var nested = RuntimeReflectionHelpers.GetMemberValue(eventRoom, memberName);
            if (nested is null)
            {
                continue;
            }

            if (RuntimeReflectionHelpers.TryGetIdentifierString(nested, EventIdMemberNames, out _) ||
                RuntimeReflectionHelpers.TryGetDisplayString(nested, EventNameMemberNames, out _))
            {
                return nested;
            }

            var resolved = ResolveEventSource(nested, depth + 1);
            if (!ReferenceEquals(resolved, nested))
            {
                return resolved;
            }
        }

        return eventRoom;
    }

    private readonly record struct EventSnapshot(string EventId, string EventName);
}
