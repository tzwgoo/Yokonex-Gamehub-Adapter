using STS2Bridge.Events;
using STS2Bridge.State;

namespace STS2Bridge.Runtime;

internal static class RunAbandonedEventBridgeLogic
{
    private static readonly string[] WasAbandonedMemberNames = ["IsAbandoned", "isAbandoned", "WasAbandoned", "wasAbandoned"];
    private static readonly string[] IsGameOverMemberNames = ["IsGameOver", "isGameOver"];

    public static bool PublishRunAbandoned(GameEventBus eventBus, GameStateStore stateStore, object? runManager)
    {
        RuntimeReflectionHelpers.TryGetBool(runManager, WasAbandonedMemberNames, out var wasAbandoned);
        RuntimeReflectionHelpers.TryGetBool(runManager, IsGameOverMemberNames, out var isGameOver);

        var state = stateStore.GetSnapshot();
        eventBus.Publish(new GameEvent(
            EventId: $"evt-{Guid.NewGuid():N}",
            Type: EventTypes.RunAbandoned,
            RunId: state.RunId,
            Floor: state.Floor,
            RoomType: state.RoomType,
            Payload: new
            {
                source = "run_manager_abandon",
                wasAbandoned = wasAbandoned || runManager is not null,
                isGameOver
            }));

        return true;
    }
}
