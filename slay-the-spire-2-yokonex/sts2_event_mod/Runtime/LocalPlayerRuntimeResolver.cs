namespace STS2Bridge.Runtime;

internal static class LocalPlayerRuntimeResolver
{
    private static readonly string[] PlayerIdMemberNames = ["PlayerId", "playerId", "NetId", "netId"];

    public static string ResolveOrKeep(string currentLocalPlayerId)
    {
        return TryResolveLocalPlayerId(out var localPlayerId)
            ? localPlayerId
            : currentLocalPlayerId;
    }

    public static bool TryResolveLocalPlayerId(out string playerId)
    {
        playerId = string.Empty;

        var runState = TryGetRunState();
        if (runState is null)
        {
            return false;
        }

        var localContextType = Type.GetType("MegaCrit.Sts2.Core.Context.LocalContext, sts2");
        if (localContextType is null)
        {
            return false;
        }

        var getMe = localContextType.GetMethod("GetMe", [runState.GetType()]);
        if (getMe is null)
        {
            return false;
        }

        object? localPlayer;
        try
        {
            localPlayer = getMe.Invoke(null, [runState]);
        }
        catch
        {
            return false;
        }

        return RuntimeReflectionHelpers.TryGetIdentifierString(localPlayer, PlayerIdMemberNames, out playerId);
    }

    private static object? TryGetRunState()
    {
        var runManagerType = Type.GetType("MegaCrit.Sts2.Core.Runs.RunManager, sts2");
        if (runManagerType is null)
        {
            return null;
        }

        var instance = runManagerType.GetProperty("Instance")?.GetValue(null);
        if (instance is null)
        {
            return null;
        }

        var debugOnlyGetState = instance.GetType().GetMethod("DebugOnlyGetState", Type.EmptyTypes);
        if (debugOnlyGetState is not null)
        {
            return debugOnlyGetState.Invoke(instance, null);
        }

        return instance.GetType().GetProperty("State")?.GetValue(instance);
    }
}
